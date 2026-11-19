using Base.Message;
using System.Text.Json.Serialization;

namespace Base.Keycloak.Events;

/// <summary>
/// Represents the <c>content</c> payload of a Keycloak one-time password command (<c>email-user-2fa-otp</c>).
/// </summary>
public class OtpEmailContent : BaseNotificationContent, IExpiringNotificationContent
{
    /// <summary>
    /// Gets the one-time password code.
    /// </summary>
    [JsonPropertyName("otpCode")]
    public required string OtpCode { get; init; }

    /// <summary>
    /// Gets the expiration timestamp for the one-time password.
    /// </summary>
    [JsonPropertyName("expiresAt")]
    [JsonConverter(typeof(UtcDateTimeOffsetJsonConverter))]
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>
    /// Gets the stated validity of the one-time password in minutes. This is the policy value the email
    /// text quotes; it is not recomputed from <see cref="ExpiresAt"/> at render time.
    /// </summary>
    [JsonPropertyName("validForMinutes")]
    public int ValidForMinutes { get; init; }

}
