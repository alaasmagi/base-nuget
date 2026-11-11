using System.Security.Claims;
using Base.Authorization;
using Microsoft.AspNetCore.Authentication;

namespace Base.Keycloak.Authentication;

/// <summary>
/// Removes permission claims added by Base.Keycloak before a cookie ticket is serialized, so permissions mapped for
/// the current request never enlarge the cookie.
/// </summary>
internal sealed class PermissionClaimsStrippingTicketDataFormat : ISecureDataFormat<AuthenticationTicket>
{
    private readonly ISecureDataFormat<AuthenticationTicket> _inner;

    internal PermissionClaimsStrippingTicketDataFormat(ISecureDataFormat<AuthenticationTicket> inner)
    {
        _inner = inner;
    }

    public string Protect(AuthenticationTicket data) => _inner.Protect(Strip(data));

    public string Protect(AuthenticationTicket data, string? purpose) => _inner.Protect(Strip(data), purpose);

    public AuthenticationTicket? Unprotect(string? protectedText) => _inner.Unprotect(protectedText);

    public AuthenticationTicket? Unprotect(string? protectedText, string? purpose) =>
        _inner.Unprotect(protectedText, purpose);

    private static AuthenticationTicket Strip(AuthenticationTicket ticket)
    {
        if (!ticket.Principal.HasClaim(IsMappedPermission))
        {
            return ticket;
        }

        var principal = new ClaimsPrincipal();
        foreach (var identity in ticket.Principal.Identities)
        {
            var clone = identity.Clone();
            foreach (var claim in clone.Claims.Where(IsMappedPermission).ToList())
            {
                clone.TryRemoveClaim(claim);
            }

            principal.AddIdentity(clone);
        }

        return new AuthenticationTicket(principal, ticket.Properties, ticket.AuthenticationScheme);
    }

    private static bool IsMappedPermission(Claim claim) =>
        claim.Type == PermissionClaimTypes.Permission
        && claim.Issuer == KeycloakPermissionClaimsTransformation.ClaimIssuer;
}
