using Base.Message;
using System.Text.Json.Serialization;

namespace Base.Keycloak.Events;

/// <summary>
/// Represents the <c>content</c> payload of a Keycloak password reset command (<c>email-user-password-reset</c>).
/// </summary>
public class PasswordResetContent : BaseNotificationContent, IExpiringNotificationContent
{
    /// <summary>
    /// Gets the Keycloak action link the user follows to reset their password.
    /// </summary>
    [JsonPropertyName("actionLink")]
    public required string ActionLink { get; init; }

    /// <summary>
    /// Gets the expiration timestamp for the action link.
    /// </summary>
    [JsonPropertyName("expiresAt")]
    [JsonConverter(typeof(UtcDateTimeOffsetJsonConverter))]
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>
    /// Gets the stated validity of the action link in minutes. This is the policy value the email text
    /// quotes; it is not recomputed from <see cref="ExpiresAt"/> at render time.
    /// </summary>
    [JsonPropertyName("validForMinutes")]
    public int ValidForMinutes { get; init; }

}
