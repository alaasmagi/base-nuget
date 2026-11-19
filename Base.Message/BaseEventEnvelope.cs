using System.Globalization;
using System.Text.Json.Serialization;
using Base.Contracts.Message;

namespace Base.Message;

/// <summary>
/// Provides the base event envelope: a strongly typed content payload plus the fixed metadata every message
/// on the platform shares. Events and commands have exactly the fields
/// <c>id, source, tenant, action, timestamp, contentVersion, content</c>, none of them ever null. Whether a message is
/// an event or a command is decided by the exchange it is published on.
/// </summary>
/// <typeparam name="TContent">The content payload type carried by the event envelope.</typeparam>
[JsonConverter(typeof(BaseEventEnvelopeJsonConverterFactory))]
public class BaseEventEnvelope<TContent> : IBaseEventEnvelope<TContent>
{
    /// <summary>
    /// Gets and/or initializes the unique message identifier (a UUID string) used as the idempotency key.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// Gets and/or initializes the publisher of the message.
    /// </summary>
    [JsonPropertyName("source")]
    public required string Source { get; init; }

    /// <summary>
    /// Gets and/or initializes the subject the message concerns (the Keycloak realm name, or the requesting
    /// product's slug for ai-hub replies).
    /// </summary>
    [JsonPropertyName("tenant")]
    public required string Tenant { get; init; }

    /// <summary>
    /// Gets and/or initializes what happened or what to send (single hyphenated segment).
    /// </summary>
    [JsonPropertyName("action")]
    public required string Action { get; init; }

    /// <summary>
    /// Gets and/or initializes the ISO-8601 UTC publish time with millisecond precision and a <c>Z</c> suffix.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public required string Timestamp { get; init; }

    /// <summary>
    /// Gets and/or initializes the content version supplied by the caller and passed through verbatim.
    /// </summary>
    [JsonPropertyName("contentVersion")]
    public required string ContentVersion { get; init; }

    /// <summary>
    /// Gets and/or initializes the content payload associated with the event. Never <c>null</c>; use an empty object
    /// when there is nothing to send.
    /// </summary>
    [JsonPropertyName("content")]
    public required TContent Content { get; init; }

    /// <summary>
    /// Creates an envelope, stamping a fresh <see cref="Id"/> (a new UUID) and the current
    /// <see cref="Timestamp"/> (<see cref="DateTimeOffset.UtcNow"/> truncated to milliseconds, ISO-8601 with a
    /// <c>Z</c> suffix). The <paramref name="contentVersion"/> is passed through verbatim. Use it for events and
    /// commands alike.
    /// </summary>
    public static BaseEventEnvelope<TContent> Create(
        string source,
        string tenant,
        string action,
        string contentVersion,
        TContent content) =>
        new()
        {
            Id = Guid.NewGuid().ToString(),
            Source = source,
            Tenant = tenant,
            Action = action,
            Timestamp = FormatTimestamp(DateTimeOffset.UtcNow),
            ContentVersion = contentVersion,
            Content = content
        };

    /// <summary>
    /// Formats an instant as the envelope timestamp: converted to UTC, truncated to millisecond precision,
    /// ISO-8601 with a <c>Z</c> suffix. Never a local time, never sub-millisecond precision.
    /// </summary>
    public static string FormatTimestamp(DateTimeOffset instant) =>
        instant.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
}
