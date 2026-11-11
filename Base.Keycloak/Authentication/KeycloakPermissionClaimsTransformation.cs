using System.Security.Claims;
using Base.Authorization;
using Microsoft.AspNetCore.Authentication;

namespace Base.Keycloak.Authentication;

/// <summary>
/// Adds the permissions from <c>resource_access.{clientId}.roles</c> to the principal as
/// <see cref="PermissionClaimTypes.Permission"/> claims. Only values containing a dot are permissions; role names
/// and roles of other clients are ignored. <see cref="PermissionClaimTypes.Permission"/> claims from any other
/// source are removed.
/// </summary>
public class KeycloakPermissionClaimsTransformation : IClaimsTransformation
{
    /// <summary>
    /// The issuer assigned to permission claims added by this transformation.
    /// </summary>
    public const string ClaimIssuer = "Base.Keycloak";

    private readonly string? _clientId;

    /// <summary>
    /// Initializes a new instance of the <see cref="KeycloakPermissionClaimsTransformation"/> class.
    /// </summary>
    /// <param name="options">The Keycloak authentication settings. The backend's own client identifier is
    /// <see cref="KeycloakOptions.ClientId"/>, or <see cref="KeycloakOptions.Audience"/> when no client identifier
    /// is set.</param>
    public KeycloakPermissionClaimsTransformation(KeycloakOptions options)
    {
        _clientId = KeycloakPermissionReader.ResolveClientId(options);
    }

    /// <summary>
    /// Transforms the principal by replacing its permission claims with the permissions granted to the configured
    /// client. Repeated runs produce the same result, and a missing or malformed <c>resource_access</c> claim
    /// results in no permissions.
    /// </summary>
    /// <param name="principal">The principal to transform.</param>
    /// <returns>A task that resolves to the transformed principal.</returns>
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        foreach (var identity in principal.Identities)
        {
            var foreignClaims = identity.FindAll(PermissionClaimTypes.Permission)
                .Where(claim => claim.Issuer != ClaimIssuer)
                .ToList();
            foreach (var claim in foreignClaims)
            {
                identity.TryRemoveClaim(claim);
            }
        }

        if (principal.Identity is not ClaimsIdentity primaryIdentity || string.IsNullOrEmpty(_clientId))
        {
            return Task.FromResult(principal);
        }

        var resourceAccessClaim = principal.FindFirst("resource_access");
        if (resourceAccessClaim == null)
        {
            return Task.FromResult(principal);
        }

        foreach (var permission in KeycloakPermissionReader.Read(resourceAccessClaim.Value, _clientId))
        {
            if (!primaryIdentity.HasClaim(PermissionClaimTypes.Permission, permission))
            {
                primaryIdentity.AddClaim(new Claim(PermissionClaimTypes.Permission, permission, ClaimValueTypes.String,
                    ClaimIssuer));
            }
        }

        return Task.FromResult(principal);
    }
}
