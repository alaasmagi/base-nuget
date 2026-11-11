namespace Base.Authorization;

/// <summary>
/// Defines the claim types the permission authorization layer reads from the principal.
/// </summary>
public static class PermissionClaimTypes
{
    /// <summary>
    /// The claim type that carries one <c>{module}.{action}</c> permission per claim.
    /// </summary>
    public const string Permission = "permission";
}
