using System.Collections.Concurrent;
using System.Text.Json;
using Base.Contracts.Message;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Base.Message.RabbitMQ;

/// <summary>
/// Publishes base event envelopes to a RabbitMQ exchange as persistent messages. The routing key is derived
/// from the envelope via <see cref="BaseRoutingKey"/>. Every publish waits for a broker confirmation (bounded
/// by <see cref="RabbitMqOptions.PublishConfirmTimeout"/>) and is sent <c>mandatory</c>, so an unroutable
/// message is surfaced to the caller rather than silently dropped. The outcome is reported through
/// <see cref="PublishResult"/>.
/// </summary>
public class RabbitMqEventPublisher : IBaseEventPublisher, IAsyncDisposable
{
    private readonly RabbitMqConnectionManager _connectionManager;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqEventPublisher> _logger;
    private readonly SemaphoreSlim _publishLock = new(1, 1);

    // MessageId -> reason, populated by the basic-return handler for messages the broker could not route.
    private readonly ConcurrentDictionary<string, string> _returned = new();
    private IChannel? _channel;

    /// <summary>
    /// Initializes a new instance of the <see cref="RabbitMqEventPublisher"/> class.
    /// </summary>
    public RabbitMqEventPublisher(
        RabbitMqConnectionManager connectionManager,
        RabbitMqOptions options,
        ILogger<RabbitMqEventPublisher> logger)
    {
        _connectionManager = connectionManager;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Publishes an event envelope, deriving the routing key from the envelope and waiting for the broker
    /// confirmation.
    /// </summary>
    public async Task<PublishResult> PublishAsync<TContent>(
        IBaseEventEnvelope<TContent> message,
        string? expiration = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        // Throws ArgumentException on a malformed segment before we touch the broker.
        var routingKey = BaseRoutingKey.For(message);
        if (message.Id is null || message.Timestamp is null || message.ContentVersion is null || message.Content is null)
            throw new ArgumentException("Envelope fields must not be null; use an empty object for empty content.",
                nameof(message));
        if (message.Content is BaseNotificationContent notification)
            notification.Validate();

        // A RabbitMQ channel is not thread-safe, so publishes are serialized on a single owned channel.
        await _publishLock.WaitAsync(cancellationToken);
        try
        {
            var channel = await GetChannelAsync(cancellationToken);
            var body = JsonSerializer.SerializeToUtf8Bytes(message, message.GetType());

            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                // The envelope id is the idempotency key; surface it as the AMQP MessageId so consumers can
                // dedupe redeliveries without deserializing the body first.
                MessageId = message.Id
            };

            if (!string.IsNullOrWhiteSpace(expiration))
            {
                properties.Expiration = expiration;
            }

            // Clear any stale return recorded for this id before we publish.
            _returned.TryRemove(message.Id, out _);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_options.PublishConfirmTimeout);

            try
            {
                // Publisher confirmation tracking is enabled on the channel, so this call completes only once
                // the broker has acked the message (and throws if it nacks).
                await channel.BasicPublishAsync(
                    exchange: _options.Exchange,
                    routingKey: routingKey,
                    mandatory: true,
                    basicProperties: properties,
                    body: body,
                    cancellationToken: timeoutCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested &&
                                                     !cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(
                    "Timed out after {Timeout} waiting for RabbitMQ confirmation of message '{MessageId}' " +
                    "(routingKey '{RoutingKey}')", _options.PublishConfirmTimeout, message.Id, routingKey);
                return PublishResult.Failed(
                    $"Timed out after {_options.PublishConfirmTimeout} waiting for broker confirmation.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Broker rejected message '{MessageId}' (routingKey '{RoutingKey}')",
                    message.Id, routingKey);
                return PublishResult.Failed($"Broker rejected the message: {ex.Message}");
            }

            // A mandatory message with no matching binding is returned by the broker before the confirm, so by
            // the time the confirm above completes the basic-return handler has already run for this id.
            if (_returned.TryRemove(message.Id, out var reason))
            {
                _logger.LogWarning("Message '{MessageId}' was unroutable: {Reason}", message.Id, reason);
                return PublishResult.Failed($"Message was unroutable: {reason}");
            }

            return PublishResult.Ok();
        }
        finally
        {
            _publishLock.Release();
        }
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        if (_channel is not null)
        {
            _channel.BasicReturnAsync -= OnBasicReturnAsync;
            await _channel.DisposeAsync();
            _channel = null;
        }

        _channel = await _connectionManager.CreateChannelAsync(
            new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true),
            cancellationToken);

        _channel.BasicReturnAsync += OnBasicReturnAsync;

        return _channel;
    }

    private Task OnBasicReturnAsync(object sender, BasicReturnEventArgs args)
    {
        var messageId = args.BasicProperties.MessageId;
        if (!string.IsNullOrEmpty(messageId))
        {
            _returned[messageId] =
                $"{args.ReplyCode} {args.ReplyText} (exchange '{args.Exchange}', routingKey '{args.RoutingKey}')";
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Asynchronously releases the publisher channel.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            _channel.BasicReturnAsync -= OnBasicReturnAsync;
            await _channel.DisposeAsync();
            _channel = null;
        }

        _publishLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
