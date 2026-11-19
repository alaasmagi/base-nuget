namespace Base.Contracts.Message;

/// <summary>
/// Defines the base event envelope contract. The envelope carries a strongly typed content payload plus the
/// fixed metadata every message on the platform shares. Events and commands carry exactly these fields, none of them
/// ever null: <c>id, source, tenant, action, timestamp, contentVersion, content</c>. Whether a message is an event or
/// a command is decided by the exchange it is published on, not by the envelope.
/// </summary>
/// <typeparam name="TContent">The content payload type carried by the event envelope.</typeparam>
public interface IBaseEventEnvelope<TContent>
{
    /// <summary>
    /// Gets and/or initializes the unique message identifier (a UUID string), one per message. It is the
    /// idempotency key consumers use to detect and drop redeliveries.
    /// </summary>
    public string Id { get; init; }

    /// <summary>
    /// Gets and/or initializes the publisher of the message: a single segment that identifies who published
    /// it (for example <c>identity-hub</c>). A plain string; the package does not validate it.
    /// </summary>
    public string Source { get; init; }

    /// <summary>
    /// Gets and/or initializes the subject the message concerns: a single segment (the Keycloak realm name, or the
    /// requesting product's slug for ai-hub replies).
    /// </summary>
    public string Tenant { get; init; }

    /// <summary>
    /// Gets and/or initializes what happened or what to send: a single hyphenated segment
    /// (for example <c>user-created</c>, <c>email-user-2fa-otp</c>).
    /// </summary>
    public string Action { get; init; }

    /// <summary>
    /// Gets and/or initializes the publish time as an ISO-8601 UTC string with millisecond precision and a
    /// <c>Z</c> suffix (for example <c>2026-07-31T12:34:56.789Z</c>).
    /// </summary>
    public string Timestamp { get; init; }

    /// <summary>
    /// Gets and/or initializes the content version supplied by the emitter and passed through verbatim. The
    /// package neither validates nor interprets it: the emitter supplies it, the consumer reads it.
    /// </summary>
    public string ContentVersion { get; init; }

    /// <summary>
    /// Gets and/or initializes the payload associated with the event. Its shape is defined per
    /// <see cref="Action"/> and <see cref="ContentVersion"/>. Never <c>null</c>; an empty object when there is nothing
    /// to send.
    /// </summary>
    public TContent Content { get; init; }
}
