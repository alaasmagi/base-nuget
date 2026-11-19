using Base.Contracts.Message;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Base.Message.RabbitMQ;

/// <summary>
/// Provides a hosted RabbitMQ listener that hands each raw delivery (routing key and body) to the subclass and
/// lets it decide whether the message is acked, requeued or rejected. Use it when a consumer needs transport
/// metadata; use <see cref="RabbitMqDeliveryConsumerBase{TContent}"/> for typed envelopes with per-message
/// requeue control, or <see cref="RabbitMqConsumerBase{TContent}"/> for ack-or-reject handling.
/// </summary>
public abstract class RabbitMqDeliveryConsumerBase : BackgroundService
{
    private readonly RabbitMqConnectionManager _connectionManager;
    private readonly ILogger _logger;
    private readonly string _queueName;

    /// <summary>
    /// Initializes a new instance of the <see cref="RabbitMqDeliveryConsumerBase"/> class. The queue and its
    /// bindings are part of the externally managed topology and must exist before the consumer starts; this
    /// class only consumes from <paramref name="queueName"/>.
    /// </summary>
    protected RabbitMqDeliveryConsumerBase(
        RabbitMqConnectionManager connectionManager,
        ILogger logger,
        string queueName)
    {
        _connectionManager = connectionManager;
        _logger = logger;
        _queueName = queueName;
    }

    /// <summary>
    /// Gets the maximum number of unacknowledged messages the broker delivers to this consumer at once.
    /// Override to tune throughput versus fairness. Defaults to 10.
    /// </summary>
    protected virtual ushort PrefetchCount => 10;

    /// <summary>
    /// Gets the disposition applied when <see cref="HandleDeliveryAsync"/> throws. Defaults to
    /// <see cref="MessageDisposition.Reject"/>; override with <see cref="MessageDisposition.Requeue"/> when
    /// unexpected failures are likely transient and processing is idempotent.
    /// </summary>
    protected virtual MessageDisposition ExceptionDisposition => MessageDisposition.Reject;

    /// <summary>
    /// Gets how long a <see cref="MessageDisposition.Requeue"/> delivery is held before it is nacked with requeue,
    /// so that immediate redelivery does not use up the queue's <c>x-delivery-limit</c> during a short outage. The
    /// delivery keeps its prefetch slot while waiting. Defaults to 5 seconds.
    /// </summary>
    protected virtual TimeSpan RequeueDelay => TimeSpan.FromSeconds(5);

    /// <summary>
    /// Processes one delivery and returns what the broker should be told about it.
    /// </summary>
    /// <param name="delivery">The raw delivery.</param>
    /// <param name="cancellationToken">A token that is cancelled when the hosted service is stopping.</param>
    /// <returns>A task that resolves to the disposition for the delivery.</returns>
    protected abstract Task<MessageDisposition> HandleDeliveryAsync(
        MessageDelivery delivery,
        CancellationToken cancellationToken);

    /// <summary>
    /// Starts consuming RabbitMQ messages from the pre-existing queue until the service is stopped. The queue
    /// and its bindings are not declared here; they are managed outside this package.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var channel = await _connectionManager.CreateChannelAsync(cancellationToken: stoppingToken);

        try
        {
            await channel.BasicQosAsync(
                prefetchSize: 0,
                prefetchCount: PrefetchCount,
                global: false,
                cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += (_, ea) => OnReceivedAsync(channel, ea, stoppingToken);

            await channel.BasicConsumeAsync(_queueName, autoAck: false, consumer, stoppingToken);

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected when the hosted service is stopping.
        }
        finally
        {
            await channel.DisposeAsync();
        }
    }

    internal async Task OnReceivedAsync(IChannel channel, BasicDeliverEventArgs ea, CancellationToken stoppingToken)
    {
        MessageDisposition disposition;

        try
        {
            disposition = await HandleDeliveryAsync(
                new MessageDelivery
                {
                    RoutingKey = ea.RoutingKey,
                    Body = ea.Body,
                    Redelivered = ea.Redelivered
                },
                stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process message from queue '{Queue}'", _queueName);
            disposition = ExceptionDisposition;
        }

        switch (disposition)
        {
            case MessageDisposition.Ack:
                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, stoppingToken);
                break;

            case MessageDisposition.Requeue:
                try
                {
                    await Task.Delay(RequeueDelay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Shutting down: the unacked delivery returns to the queue when the channel closes.
                    return;
                }

                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true, CancellationToken.None);
                break;

            default:
                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, stoppingToken);
                break;
        }
    }
}
