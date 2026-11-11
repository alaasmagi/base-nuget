namespace Base.Authorization;

/// <summary>
/// Builds and parses the authorization policy names used for permission checks.
/// </summary>
public static class PermissionPolicyNames
{
    /// <summary>
    /// The prefix that marks a policy name as a permission policy.
    /// </summary>
    public const string Prefix = "perm:";

    /// <summary>
    /// Builds the policy name for the <c>{module}.{action}</c> permission.
    /// </summary>
    /// <param name="module">The permission module, for example <c>courses</c>.</param>
    /// <param name="action">The permission action, for example <see cref="Actions.Create"/>.</param>
    /// <returns>The policy name <c>perm:{module}.{action}</c>.</returns>
    public static string For(string module, string action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(module);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        return $"{Prefix}{module}.{action}";
    }

    /// <summary>
    /// Builds the policy name for a full <c>{module}.{action}</c> permission.
    /// </summary>
    /// <param name="permission">The permission, for example <c>courses.create</c>.</param>
    /// <returns>The policy name <c>perm:{permission}</c>.</returns>
    /// <exception cref="ArgumentException">The permission is empty or does not contain a dot.</exception>
    public static string For(string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        if (!permission.Contains('.'))
        {
            throw new ArgumentException("A permission must use the '{module}.{action}' format.", nameof(permission));
        }

        return Prefix + permission;
    }

    /// <summary>
    /// Extracts the permission from a permission policy name.
    /// </summary>
    /// <param name="policyName">The policy name to parse.</param>
    /// <param name="permission">The permission when the name is a permission policy; otherwise <c>null</c>.</param>
    /// <returns><see langword="true"/> when <paramref name="policyName"/> is a non-empty permission policy name.</returns>
    public static bool TryParse(string? policyName, out string? permission)
    {
        permission = null;
        if (policyName == null || !policyName.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var value = policyName[Prefix.Length..];
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        permission = value;
        return true;
    }
}
