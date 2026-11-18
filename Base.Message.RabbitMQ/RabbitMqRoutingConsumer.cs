using Base.Contracts.Message;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Base.Message.RabbitMQ;

/// <summary>
/// Provides a hosted RabbitMQ listener that reads every message from one pre-existing queue and hands each delivery to
/// the typed handler its <see cref="RabbitMqEventRouter"/> route selects, in a fresh DI scope. Register it with
/// <see cref="ServiceCollectionExtensions.AddRabbitMqRoutingConsumer"/>.
/// </summary>
public class RabbitMqRoutingConsumer : RabbitMqDeliveryConsumerBase
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqEventRouter _router;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RabbitMqRoutingConsumer"/> class. The queue and its bindings are part
    /// of the externally managed topology and must exist before the consumer starts.
    /// </summary>
    public RabbitMqRoutingConsumer(
        RabbitMqConnectionManager connectionManager,
        IServiceScopeFactory scopeFactory,
        RabbitMqEventRouter router,
        ILogger<RabbitMqRoutingConsumer> logger,
        string queueName)
        : base(connectionManager, logger, queueName)
    {
        _scopeFactory = scopeFactory;
        _router = router;
        _logger = logger;
    }

    /// <summary>
    /// Routes the delivery to its handler in a new DI scope.
    /// </summary>
    protected override async Task<MessageDisposition> HandleDeliveryAsync(
        MessageDelivery delivery,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        return await _router.RouteAsync(delivery, scope.ServiceProvider, _logger, cancellationToken);
    }
}
