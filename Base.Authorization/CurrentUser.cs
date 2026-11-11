using System.Security.Claims;
using Base.Contracts.Authorization;
using Microsoft.AspNetCore.Http;

namespace Base.Authorization;

/// <summary>
/// Reads the current caller from the request <see cref="ClaimsPrincipal"/>. Permissions come exclusively from
/// <see cref="PermissionClaimTypes.Permission"/> claims.
/// </summary>
public class CurrentUser : ICurrentUser
{
    private const string SubjectClaimType = "sub";
    private const string EmailClaimType = "email";
    private const string NameClaimType = "name";
    private const string ClientIdClaimType = "client_id";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private ClaimsPrincipal? _permissionsPrincipal;
    private IReadOnlySet<string> _permissions = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="CurrentUser"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The accessor for the current HTTP context.</param>
    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true && TryGetId(out _);

    /// <inheritdoc />
    public Guid Id => TryGetId(out var id)
        ? id
        : throw new InvalidOperationException("The current principal has no valid 'sub' claim.");

    /// <inheritdoc />
    public string? Email => FindValue(EmailClaimType, ClaimTypes.Email);

    /// <inheritdoc />
    public string? Name => FindValue(NameClaimType, ClaimTypes.Name);

    /// <inheritdoc />
    public bool IsService => FindValue(ClientIdClaimType) != null && Email == null;

    /// <inheritdoc />
    public string? ClientId => IsService ? FindValue(ClientIdClaimType) : null;

    /// <inheritdoc />
    public IReadOnlySet<string> Permissions
    {
        get
        {
            var principal = Principal;
            if (!ReferenceEquals(principal, _permissionsPrincipal))
            {
                _permissions = principal == null
                    ? new HashSet<string>(StringComparer.Ordinal)
                    : principal.FindAll(PermissionClaimTypes.Permission)
                        .Select(claim => claim.Value)
                        .ToHashSet(StringComparer.Ordinal);
                _permissionsPrincipal = principal;
            }

            return _permissions;
        }
    }

    /// <inheritdoc />
    public bool HasPermission(string module, string action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(module);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        return Permissions.Contains($"{module}.{action}");
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    private bool TryGetId(out Guid id)
    {
        return Guid.TryParse(FindValue(SubjectClaimType, ClaimTypes.NameIdentifier), out id);
    }

    private string? FindValue(params string[] claimTypes)
    {
        var principal = Principal;
        if (principal == null)
        {
            return null;
        }

        foreach (var claimType in claimTypes)
        {
            var value = principal.FindFirst(claimType)?.Value;
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }
        }

        return null;
    }
}
