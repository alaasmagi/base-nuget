using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Base.Authorization;

/// <summary>
/// Resolves <c>perm:{permission}</c> policy names to policies that require an authenticated user with a matching
/// <see cref="PermissionClaimTypes.Permission"/> claim. Other policy names are resolved by the default provider.
/// </summary>
public class PermissionPolicyProvider : DefaultAuthorizationPolicyProvider
{
    private readonly ConcurrentDictionary<string, AuthorizationPolicy> _policies = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="PermissionPolicyProvider"/> class.
    /// </summary>
    /// <param name="options">The authorization options used for non-permission policies.</param>
    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : base(options)
    {
    }

    /// <inheritdoc />
    public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!PermissionPolicyNames.TryParse(policyName, out var permission))
        {
            return base.GetPolicyAsync(policyName);
        }

        var policy = _policies.GetOrAdd(permission!, static value => new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireClaim(PermissionClaimTypes.Permission, value)
            .Build());

        return Task.FromResult<AuthorizationPolicy?>(policy);
    }
}
