using Microsoft.AspNetCore.Authorization;

namespace Base.Authorization;

/// <summary>
/// Requires the caller to hold a <c>{module}.{action}</c> permission. Applying the attribute several times
/// requires all listed permissions.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public class RequirePermissionAttribute : AuthorizeAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RequirePermissionAttribute"/> class for the
    /// <c>{module}.{action}</c> permission.
    /// </summary>
    /// <param name="module">The permission module, for example <c>courses</c>.</param>
    /// <param name="action">The permission action, for example <see cref="Actions.Create"/>.</param>
    public RequirePermissionAttribute(string module, string action)
        : base(PermissionPolicyNames.For(module, action))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RequirePermissionAttribute"/> class for a full
    /// <c>{module}.{action}</c> permission.
    /// </summary>
    /// <param name="permission">The permission, for example <c>courses.create</c>.</param>
    public RequirePermissionAttribute(string permission)
        : base(PermissionPolicyNames.For(permission))
    {
    }
}
