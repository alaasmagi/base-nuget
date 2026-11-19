using Base.Contracts.Message;

namespace Base.Message;

/// <summary>
/// Builds the one and only routing-key shape used across the platform: <c>{source}.{tenant}.{action}</c>
/// (for example <c>identity-hub.my-realm.email-user-2fa-otp</c>). This is the single place a routing key is
/// constructed; nothing else in the package composes one.
/// </summary>
public static class BaseRoutingKey
{
    /// <summary>
    /// Builds the routing key <c>{source}.{tenant}.{action}</c> from the envelope.
    /// </summary>
    /// <typeparam name="TContent">The content payload type carried by the envelope.</typeparam>
    /// <param name="envelope">The event envelope to derive the routing key from.</param>
    /// <returns>The routing key.</returns>
    /// <exception cref="ArgumentException">
    /// Any of <see cref="IBaseEventEnvelope{TContent}.Source"/>, <see cref="IBaseEventEnvelope{TContent}.Tenant"/>
    /// or <see cref="IBaseEventEnvelope{TContent}.Action"/> is null, empty/whitespace, or contains a dot. A dot
    /// in a segment shifts every later segment and silently breaks consumer bindings, so it is rejected here.
    /// </exception>
    public static string For<TContent>(IBaseEventEnvelope<TContent> envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var source = Validate(envelope.Source, nameof(envelope.Source));
        var tenant = Validate(envelope.Tenant, nameof(envelope.Tenant));
        var action = Validate(envelope.Action, nameof(envelope.Action));

        return $"{source}.{tenant}.{action}";
    }

    private static string Validate(string segment, string name)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            throw new ArgumentException($"Routing key segment '{name}' must not be null or empty.", name);
        }

        if (segment.Contains('.'))
        {
            throw new ArgumentException(
                $"Routing key segment '{name}' must not contain a dot (was '{segment}').", name);
        }

        return segment;
    }
}
