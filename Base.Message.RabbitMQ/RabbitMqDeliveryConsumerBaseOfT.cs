using Base.Contracts.Message;
using Microsoft.Extensions.Logging;

namespace Base.Message.RabbitMQ;

/// <summary>
/// Provides a hosted RabbitMQ listener that deserializes each delivery into a
/// <see cref="BaseEventEnvelope{TContent}"/> and lets the subclass return the <see cref="MessageDisposition"/>
/// (<see cref="MessageDisposition.Ack"/>, <see cref="MessageDisposition.Requeue"/>,
/// <see cref="MessageDisposition.Reject"/>) per message. A delivery that is not a valid envelope (malformed JSON, a
/// missing or <c>null</c> envelope field, content of the wrong shape) is rejected without reaching the subclass.
/// Return <see cref="MessageDisposition.Requeue"/> only for transient failures; it is nacked after
/// <see cref="RabbitMqDeliveryConsumerBase.RequeueDelay"/>.
/// </summary>
/// <typeparam name="TContent">The content payload type carried by the consumed event envelopes.</typeparam>
public abstract class RabbitMqDeliveryConsumerBase<TContent> : RabbitMqDeliveryConsumerBase
{
    private readonly ILogger _logger;
    private readonly string _queueName;

    /// <summary>
    /// Initializes a new instance of the <see cref="RabbitMqDeliveryConsumerBase{TContent}"/> class. The queue and
    /// its bindings are part of the externally managed topology and must exist before the consumer starts; this
    /// class only consumes from <paramref name="queueName"/>.
    /// </summary>
    protected RabbitMqDeliveryConsumerBase(
        RabbitMqConnectionManager connectionManager,
        ILogger logger,
        string queueName)
        : base(connectionManager, logger, queueName)
    {
        _logger = logger;
        _queueName = queueName;
    }

    /// <summary>
    /// Processes one envelope and returns what the broker should be told about it.
    /// </summary>
    /// <param name="envelope">The deserialized envelope.</param>
    /// <param name="cancellationToken">A token that is cancelled when the hosted service is stopping.</param>
    /// <returns>A task that resolves to the disposition for the delivery.</returns>
    protected abstract Task<MessageDisposition> HandleAsync(
        BaseEventEnvelope<TContent> envelope,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deserializes the delivery and passes the envelope to <see cref="HandleAsync"/>; rejects an invalid envelope.
    /// </summary>
    protected sealed override Task<MessageDisposition> HandleDeliveryAsync(
        MessageDelivery delivery,
        CancellationToken cancellationToken)
    {
        if (!EnvelopeReader.TryRead<TContent>(delivery, out var envelope))
        {
            _logger.LogWarning("Rejecting invalid envelope from queue '{Queue}' (routing key '{RoutingKey}')",
                _queueName, delivery.RoutingKey);
            return Task.FromResult(MessageDisposition.Reject);
        }

        return HandleAsync(envelope, cancellationToken);
    }
}
