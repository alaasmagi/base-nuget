using Base.Contracts.Message;
using Microsoft.Extensions.Logging;

namespace Base.Message.RabbitMQ;

/// <summary>
/// Provides a hosted RabbitMQ listener that consumes event envelopes with a strongly typed content payload from a queue.
/// A message is acked once the handler returns and rejected without requeue (dead-lettered) if deserialization or the
/// handler throws. Use <see cref="RabbitMqDeliveryConsumerBase{TContent}"/> for per-message requeue control.
/// </summary>
/// <typeparam name="TContent">The content payload type carried by the consumed event envelopes.</typeparam>
public abstract class RabbitMqConsumerBase<TContent> : RabbitMqDeliveryConsumerBase<TContent>
{
    private readonly IBaseEventHandler<TContent> _handler;

    /// <summary>
    /// Initializes a new instance of the <see cref="RabbitMqConsumerBase{TContent}"/> class. The queue and its
    /// bindings are part of the externally managed topology and must exist before the consumer starts; this
    /// class only consumes from <paramref name="queueName"/>.
    /// </summary>
    protected RabbitMqConsumerBase(
        RabbitMqConnectionManager connectionManager,
        IBaseEventHandler<TContent> handler,
        ILogger logger,
        string queueName)
        : base(connectionManager, logger, queueName)
    {
        _handler = handler;
    }

    /// <summary>
    /// Always <see cref="MessageDisposition.Reject"/>: a failed message is dead-lettered, never requeued.
    /// </summary>
    protected sealed override MessageDisposition ExceptionDisposition => MessageDisposition.Reject;

    /// <summary>
    /// Passes the envelope to the registered handler and acks once it returns.
    /// </summary>
    protected sealed override async Task<MessageDisposition> HandleAsync(
        BaseEventEnvelope<TContent> envelope,
        CancellationToken cancellationToken)
    {
        await _handler.HandleAsync(envelope, cancellationToken);
        return MessageDisposition.Ack;
    }
}
