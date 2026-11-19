using System.Text.Json.Serialization;

namespace Base.Keycloak.Events;

/// <summary>
/// Represents the <c>content</c> payload of a Keycloak user lifecycle event
/// (<c>user-created</c>, <c>user-updated</c>, <c>user-enabled</c>, <c>user-disabled</c>, <c>user-deleted</c>).
/// <c>user-deleted</c> carries only <see cref="UserId"/>; every other action carries all fields, with
/// <see cref="Locale"/> and <see cref="TimeZone"/> always filled by the Keycloak extension.
/// </summary>
public class UserEventContent
{
    /// <summary>
    /// Gets the Keycloak user identifier.
    /// </summary>
    [JsonPropertyName("userId")]
    public required string UserId { get; init; }

    /// <summary>
    /// Gets the user's email address, when supplied.
    /// </summary>
    [JsonPropertyName("email")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Email { get; init; }

    /// <summary>
    /// Gets the user's user name, when supplied.
    /// </summary>
    [JsonPropertyName("username")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Username { get; init; }

    /// <summary>
    /// Gets the user's full name, when supplied.
    /// </summary>
    [JsonPropertyName("fullName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FullName { get; init; }

    /// <summary>
    /// Gets the user's BCP 47 locale with region (for example <c>et-EE</c>), when supplied.
    /// </summary>
    [JsonPropertyName("locale")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Locale { get; init; }

    /// <summary>
    /// Gets the user's IANA time zone identifier (for example <c>Europe/Tallinn</c>), when supplied.
    /// </summary>
    [JsonPropertyName("timeZone")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TimeZone { get; init; }
}

