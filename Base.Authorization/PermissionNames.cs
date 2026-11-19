namespace Base.Authorization;

/// <summary>
/// Defines the permission name rule shared by policy names and token permission mapping.
/// </summary>
public static class PermissionNames
{
    /// <summary>
    /// Determines whether a value is a permission: <c>{module}.{action}</c> with exactly one dot and a non-empty
    /// module and action. Keycloak role names contain no dot and are therefore never permissions.
    /// </summary>
    /// <param name="value">The value to check, for example <c>courses.create</c>.</param>
    /// <returns><see langword="true"/> when <paramref name="value"/> is a permission; otherwise
    /// <see langword="false"/>.</returns>
    public static bool IsValid(string? value)
    {
        if (value == null)
        {
            return false;
        }

        var dot = value.IndexOf('.');
        return dot >= 0
               && value.IndexOf('.', dot + 1) < 0
               && !string.IsNullOrWhiteSpace(value[..dot])
               && !string.IsNullOrWhiteSpace(value[(dot + 1)..]);
    }
}
