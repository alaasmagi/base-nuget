namespace Base.Contracts.Authorization;

/// <summary>
/// Describes the authenticated caller of the current request, as resolved from the access token.
/// </summary>
public interface ICurrentUser
{
    /// <summary>
    /// Gets a value indicating whether the request has an authenticated principal with a valid UUID <c>sub</c>
    /// claim, so that reading <see cref="Id"/> succeeds.
    /// </summary>
    public bool IsAuthenticated { get; }

    /// <summary>
    /// Gets the caller identifier taken from the token <c>sub</c> claim. This is the only user identifier;
    /// never identify users by <see cref="Email"/> or <see cref="Name"/>.
    /// </summary>
    /// <remarks>
    /// Throws <see cref="InvalidOperationException"/> when the request has no valid <c>sub</c> claim. Callers on
    /// <c>[AllowAnonymous]</c> endpoints must check <see cref="IsAuthenticated"/> before reading this property.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The request has no <c>sub</c> claim containing a UUID.</exception>
    public Guid Id { get; }

    /// <summary>
    /// Gets the caller's email address for display purposes, or <c>null</c> when the token has none.
    /// </summary>
    public string? Email { get; }

    /// <summary>
    /// Gets the caller's display name, or <c>null</c> when the token has none.
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// Gets a value indicating whether the caller is a service (client credentials token): the token carries
    /// <c>client_id</c> and no <c>email</c>.
    /// </summary>
    public bool IsService { get; }

    /// <summary>
    /// Gets the calling client identifier for service tokens, or <c>null</c> for user tokens.
    /// </summary>
    public string? ClientId { get; }

    /// <summary>
    /// Gets the caller's permissions in <c>{module}.{action}</c> format.
    /// </summary>
    public IReadOnlySet<string> Permissions { get; }

    /// <summary>
    /// Determines whether the caller holds the <c>{module}.{action}</c> permission.
    /// </summary>
    /// <param name="module">The permission module, for example <c>courses</c>.</param>
    /// <param name="action">The permission action, for example <c>create</c>.</param>
    /// <returns><see langword="true"/> when the permission is granted; otherwise <see langword="false"/>.</returns>
    public bool HasPermission(string module, string action);
}
