using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Base.Contracts.Message;

namespace Base.Message.RabbitMQ;

/// <summary>
/// Deserializes a delivery body into a <see cref="BaseEventEnvelope{TContent}"/> without throwing, so consumers can
/// reject invalid messages instead of treating them as handler failures.
/// </summary>
internal static class EnvelopeReader
{
    /// <summary>
    /// Reads the envelope. Returns <see langword="false"/> for malformed JSON, a <c>null</c> body, a missing or
    /// <c>null</c> envelope field, or content that does not match <typeparamref name="TContent"/>.
    /// </summary>
    internal static bool TryRead<TContent>(
        MessageDelivery delivery,
        [NotNullWhen(true)] out BaseEventEnvelope<TContent>? envelope)
    {
        try
        {
            envelope = JsonSerializer.Deserialize<BaseEventEnvelope<TContent>>(delivery.Body.Span);
        }
        catch (JsonException)
        {
            envelope = null;
        }

        return envelope != null;
    }
}
