namespace Base.Authorization;

/// <summary>
/// Provides settings for <see cref="PermissionAuthorizationExtensions.AddPermissionAuthorization"/>.
/// </summary>
public class PermissionAuthorizationOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether every route endpoint is checked at startup and the application
    /// fails to start when an endpoint has no permission requirement, <c>[Authorize]</c>, or
    /// <c>[AllowAnonymous]</c>. Defaults to <c>false</c>.
    /// </summary>
    public bool ValidateEndpointsOnStartup { get; set; }
}
