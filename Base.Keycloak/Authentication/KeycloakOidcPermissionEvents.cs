using System.Globalization;
using System.Security.Claims;
using Base.Authorization;
using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.OpenIdConnect;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Base.Keycloak.Authentication;

/// <summary>
/// Maps permissions for cookie (OpenID Connect) requests from the access token stored in the cookie ticket, never
/// from the ID token. The access token is refreshed when it is expired or about to expire; requests without a usable
/// access token are rejected and signed out.
/// </summary>
internal static class KeycloakOidcPermissionEvents
{
    private const string AccessTokenName = "access_token";
    private const string RefreshTokenName = "refresh_token";
    private const string IdTokenName = "id_token";
    private const string ExpiresAtName = "expires_at";
    private const string ResourceAccessClaimType = "resource_access";

    private static readonly TimeSpan RefreshBeforeExpiration = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Chains the permission mapping after any existing <c>OnValidatePrincipal</c> handler and keeps permission
    /// claims out of the serialized cookie.
    /// </summary>
    internal static void Configure(CookieAuthenticationOptions cookie, KeycloakOptions options)
    {
        var previous = cookie.Events.OnValidatePrincipal;
        cookie.Events.OnValidatePrincipal = async context =>
        {
            await previous(context);
            if (context.Principal != null)
            {
                await ValidatePrincipalAsync(context, options);
            }
        };

        if (cookie.TicketDataFormat != null)
        {
            cookie.TicketDataFormat = new PermissionClaimsStrippingTicketDataFormat(cookie.TicketDataFormat);
        }
    }

    private static async Task ValidatePrincipalAsync(CookieValidatePrincipalContext context, KeycloakOptions options)
    {
        var token = ReadToken(context.Properties.GetTokenValue(AccessTokenName));
        var now = context.Options.TimeProvider?.GetUtcNow() ?? DateTimeOffset.UtcNow;

        if (token != null && token.ValidTo <= now.UtcDateTime.Add(RefreshBeforeExpiration))
        {
            token = ReadToken(await RefreshAsync(context));
        }

        if (token == null)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(context.Scheme.Name);
            return;
        }

        var clientId = KeycloakPermissionReader.ResolveClientId(options);
        var permissions = !string.IsNullOrEmpty(clientId) && token.TryGetClaim(ResourceAccessClaimType, out var claim)
            ? KeycloakPermissionReader.Read(claim.Value, clientId)
            : [];

        var principal = context.Principal!.Clone();
        foreach (var identity in principal.Identities)
        {
            var replacedClaims = identity.Claims
                .Where(c => c.Type is PermissionClaimTypes.Permission or ResourceAccessClaimType)
                .ToList();
            foreach (var replacedClaim in replacedClaims)
            {
                identity.TryRemoveClaim(replacedClaim);
            }
        }

        if (principal.Identity is ClaimsIdentity primaryIdentity)
        {
            foreach (var permission in permissions.Distinct(StringComparer.Ordinal))
            {
                primaryIdentity.AddClaim(new Claim(PermissionClaimTypes.Permission, permission, ClaimValueTypes.String,
                    KeycloakPermissionClaimsTransformation.ClaimIssuer));
            }
        }

        context.ReplacePrincipal(principal);
    }

    private static JsonWebToken? ReadToken(string? accessToken)
    {
        if (string.IsNullOrEmpty(accessToken))
        {
            return null;
        }

        try
        {
            // Not re-validated: the token came from the token endpoint over the back channel and is stored in the
            // encrypted cookie ticket.
            return new JsonWebTokenHandler().ReadJsonWebToken(accessToken);
        }
        catch (Exception exception) when (exception is ArgumentException or SecurityTokenMalformedException)
        {
            return null;
        }
    }

    private static async Task<string?> RefreshAsync(CookieValidatePrincipalContext context)
    {
        var services = context.HttpContext.RequestServices;
        var endpoint = services.GetService<IOpenIdConnectUserTokenEndpoint>();
        var refreshTokenValue = context.Properties.GetTokenValue(RefreshTokenName);
        if (endpoint == null || string.IsNullOrEmpty(refreshTokenValue)
            || !RefreshToken.TryParse(refreshTokenValue, out var refreshToken, out _) || refreshToken == null)
        {
            return null;
        }

        var userRefreshToken = new UserRefreshToken(refreshToken.Value, null);
        var parameters = new UserTokenRequestParameters();
        var cancellationToken = context.HttpContext.RequestAborted;
        var concurrencyControl = services.GetService<IUserTokenRequestConcurrencyControl>();

        TokenResult<UserToken> result;
        try
        {
            result = concurrencyControl == null
                ? await endpoint.RefreshAccessTokenAsync(userRefreshToken, parameters, cancellationToken)
                : await concurrencyControl.ExecuteWithConcurrencyControlAsync(userRefreshToken,
                    () => endpoint.RefreshAccessTokenAsync(userRefreshToken, parameters, cancellationToken),
                    cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }

        if (!result.WasSuccessful(out var userToken))
        {
            return null;
        }

        var tokens = context.Properties.GetTokens().ToDictionary(t => t.Name, t => t.Value, StringComparer.Ordinal);
        tokens[AccessTokenName] = userToken.AccessToken.ToString();
        tokens[ExpiresAtName] = userToken.Expiration.ToString("o", CultureInfo.InvariantCulture);
        if (userToken.RefreshToken is { } renewedRefreshToken)
        {
            tokens[RefreshTokenName] = renewedRefreshToken.ToString();
        }

        if (userToken.IdentityToken is { } identityToken)
        {
            tokens[IdTokenName] = identityToken.ToString();
        }

        context.Properties.StoreTokens(tokens.Select(t => new AuthenticationToken { Name = t.Key, Value = t.Value }));
        context.ShouldRenew = true;

        return tokens[AccessTokenName];
    }
}
