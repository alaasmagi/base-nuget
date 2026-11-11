namespace Base.Keycloak.Authentication;

/// <summary>
/// Provides Keycloak authentication settings for JWT bearer and OpenID Connect registration.
/// </summary>
public class KeycloakOptions
{
    /// <summary>
    /// Gets the Keycloak authority URL.
    /// </summary>
    public required string Authority { get; init; }

    /// <summary>
    /// Gets the expected JWT audience, normally the backend's own client identifier. When <c>null</c>, JWT bearer
    /// falls back to <see cref="ClientId"/>.
    /// </summary>
    public string? Audience { get; init; }

    /// <summary>
    /// Gets the Keycloak client identifier.
    /// </summary>
    public string? ClientId { get; init; }

    /// <summary>
    /// Gets the Keycloak client secret used by OpenID Connect flows.
    /// </summary>
    public string? ClientSecret { get; init; }

    /// <summary>
    /// Gets a value indicating whether HTTPS metadata is required.
    /// </summary>
    public bool RequireHttpsMetadata { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether client roles should be added as role claims.
    /// </summary>
    public bool IncludeClientRoles { get; init; } = true;

    /// <summary>
    /// Gets the OpenID Connect scopes to request. When <c>null</c>, the framework defaults (<c>openid</c>,
    /// <c>profile</c>) plus <c>offline_access</c> are requested; otherwise exactly these scopes are requested.
    /// </summary>
    public IReadOnlyCollection<string>? Scopes { get; init; }

    /// <summary>
    /// Gets the cookie login path. When <c>null</c>, the cookie authentication default is used.
    /// </summary>
    public string? LoginPath { get; init; }

    /// <summary>
    /// Gets the cookie logout path. When <c>null</c>, the cookie authentication default is used.
    /// </summary>
    public string? LogoutPath { get; init; }

    /// <summary>
    /// Gets the cookie access-denied path. When <c>null</c>, the cookie authentication default is used.
    /// </summary>
    public string? AccessDeniedPath { get; init; }

    /// <summary>
    /// Gets the claim type used as the user's name. When <c>null</c>, OpenID Connect uses
    /// <c>preferred_username</c> and JWT bearer keeps the framework default.
    /// </summary>
    public string? NameClaimType { get; init; }

    /// <summary>
    /// Gets the claim type used for role checks. When <c>null</c>, the framework default is used.
    /// </summary>
    public string? RoleClaimType { get; init; }

    /// <summary>
    /// Gets a value indicating whether inbound JWT claim types are mapped to their legacy
    /// <c>ClaimTypes</c> equivalents. Defaults to <c>true</c> (the framework default).
    /// </summary>
    public bool MapInboundClaims { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether pushed authorization requests (PAR) are disabled for OpenID Connect.
    /// Defaults to <c>false</c>, which uses PAR when the server advertises it.
    /// </summary>
    public bool DisablePushedAuthorization { get; init; }

    /// <summary>
    /// Gets a value indicating whether JWT bearer validates the token audience. Defaults to <c>true</c>.
    /// </summary>
    public bool ValidateAudience { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether OpenID Connect registers Duende access token management for token
    /// refresh. Defaults to <c>true</c>.
    /// </summary>
    public bool UseAccessTokenManagement { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether <see cref="KeycloakPermissionClaimsTransformation"/> replaces
    /// <see cref="KeycloakRoleClaimsTransformation"/>, so the principal carries <c>permission</c> claims for the
    /// backend's own client instead of role claims. Defaults to <c>false</c>.
    /// </summary>
    public bool MapPermissions { get; init; }

    /// <summary>
    /// Gets the clock skew JWT bearer allows when validating token lifetime. Defaults to 30 seconds, the maximum
    /// the platform token contract permits.
    /// </summary>
    public TimeSpan ClockSkew { get; init; } = TimeSpan.FromSeconds(30);
}
