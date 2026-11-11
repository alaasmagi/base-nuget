using Duende.AccessTokenManagement.OpenIdConnect;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;

namespace Base.Keycloak.Authentication;

/// <summary>
/// Provides dependency injection helpers for Keycloak OpenID Connect authentication.
/// </summary>
public static class KeycloakOidcExtensions
{
    /// <summary>
    /// Registers cookie and OpenID Connect authentication configured for Keycloak.
    /// </summary>
    /// <param name="services">The service collection to register authentication with.</param>
    /// <param name="options">The Keycloak authentication settings.</param>
    /// <returns>The authentication builder created by the registration.</returns>
    public static AuthenticationBuilder AddKeycloakOidc(this IServiceCollection services, KeycloakOptions options)
    {
        services.AddSingleton(options);
        services.AddKeycloakClaimsTransformation(options);

        if (options.UseAccessTokenManagement)
        {
            services.AddOpenIdConnectAccessTokenManagement();
        }

        var builder = services.AddAuthentication(auth =>
            {
                auth.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                auth.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie(cookie =>
            {
                if (options.LoginPath != null)
                {
                    cookie.LoginPath = options.LoginPath;
                }

                if (options.LogoutPath != null)
                {
                    cookie.LogoutPath = options.LogoutPath;
                }

                if (options.AccessDeniedPath != null)
                {
                    cookie.AccessDeniedPath = options.AccessDeniedPath;
                }
            })
            .AddOpenIdConnect(oidc =>
            {
                oidc.Authority = options.Authority;
                oidc.ClientId = options.ClientId;
                oidc.ClientSecret = options.ClientSecret;
                oidc.RequireHttpsMetadata = options.RequireHttpsMetadata;
                oidc.ResponseType = "code";
                oidc.SaveTokens = true;
                oidc.GetClaimsFromUserInfoEndpoint = true;
                oidc.MapInboundClaims = options.MapInboundClaims;

                if (options.Scopes == null)
                {
                    oidc.Scope.Add("offline_access");
                }
                else
                {
                    oidc.Scope.Clear();
                    foreach (var scope in options.Scopes)
                    {
                        oidc.Scope.Add(scope);
                    }
                }

                if (options.DisablePushedAuthorization)
                {
                    oidc.PushedAuthorizationBehavior = PushedAuthorizationBehavior.Disable;
                }

                oidc.TokenValidationParameters.NameClaimType = options.NameClaimType ?? "preferred_username";

                if (options.RoleClaimType != null)
                {
                    oidc.TokenValidationParameters.RoleClaimType = options.RoleClaimType;
                }
            });

        if (options.MapPermissions)
        {
            // Registered after AddCookie so it runs after the cookie defaults and application cookie configuration,
            // chaining any existing OnValidatePrincipal handler and wrapping the final ticket data format.
            services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
                .PostConfigure(cookie => KeycloakOidcPermissionEvents.Configure(cookie, options));
        }

        return builder;
    }
}
