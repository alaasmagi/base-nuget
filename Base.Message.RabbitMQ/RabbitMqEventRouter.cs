using System.Text.Json;
using Base.Contracts.Message;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Base.Message.RabbitMQ;

/// <summary>
/// Routes raw deliveries from one queue to typed <see cref="IBaseEventHandler{TContent}"/> handlers by the routing key's
/// <c>source</c> (and optionally <c>action</c>) segment, so a service can read every message it receives from a single
/// queue. A delivery whose envelope does not match its routing key, or that has no route, is rejected.
/// </summary>
public class RabbitMqEventRouter
{
    private readonly Dictionary<(string Source, string? Action), Func<MessageDelivery, IServiceProvider, CancellationToken, Task<bool>>> _routes = new();

    /// <summary>
    /// Routes deliveries whose routing key starts with <paramref name="source"/> to the registered
    /// <see cref="IBaseEventHandler{TContent}"/>. With <paramref name="action"/>, only that action is routed; an
    /// action-specific route wins over a source-wide one.
    /// </summary>
    /// <typeparam name="TContent">The content payload type the matching envelopes carry.</typeparam>
    /// <param name="source">The publisher slug, i.e. the first routing-key segment.</param>
    /// <param name="action">The optional action, i.e. the last routing-key segment.</param>
    /// <returns>The same router so additional routes can be chained.</returns>
    public RabbitMqEventRouter Route<TContent>(string source, string? action = null)
    {
        ValidateSegment(source, nameof(source));
        if (action != null)
        {
            ValidateSegment(action, nameof(action));
        }

        if (!_routes.TryAdd((source, action), DispatchAsync<TContent>))
        {
            throw new ArgumentException($"A route for source '{source}' and action '{action ?? "*"}' already exists.");
        }

        return this;
    }

    /// <summary>
    /// Deserializes the delivery for its route, checks that the envelope's source, tenant and action equal the routing
    /// key (broker permissions apply to the key only), and calls the handler resolved from <paramref name="services"/>.
    /// </summary>
    /// <param name="delivery">The raw delivery.</param>
    /// <param name="services">The (scoped) service provider the handler is resolved from.</param>
    /// <param name="logger">The logger used for rejected deliveries.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    /// <returns><see cref="MessageDisposition.Ack"/> once handled, otherwise <see cref="MessageDisposition.Reject"/>.</returns>
    public async Task<MessageDisposition> RouteAsync(
        MessageDelivery delivery,
        IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var segments = delivery.RoutingKey.Split('.');
        if (segments.Length != 3)
        {
            logger.LogWarning("Rejecting message with malformed routing key '{RoutingKey}'", delivery.RoutingKey);
            return MessageDisposition.Reject;
        }

        if (!_routes.TryGetValue((segments[0], segments[2]), out var dispatch) &&
            !_routes.TryGetValue((segments[0], null), out dispatch))
        {
            logger.LogWarning("Rejecting message with no route for routing key '{RoutingKey}'", delivery.RoutingKey);
            return MessageDisposition.Reject;
        }

        if (!await dispatch(delivery, services, cancellationToken))
        {
            logger.LogWarning(
                "Rejecting message whose envelope does not match routing key '{RoutingKey}'", delivery.RoutingKey);
            return MessageDisposition.Reject;
        }

        return MessageDisposition.Ack;
    }

    private static async Task<bool> DispatchAsync<TContent>(
        MessageDelivery delivery,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var envelope = JsonSerializer.Deserialize<BaseEventEnvelope<TContent>>(delivery.Body.Span);
        if (envelope == null || delivery.RoutingKey != $"{envelope.Source}.{envelope.Tenant}.{envelope.Action}")
        {
            return false;
        }

        await services.GetRequiredService<IBaseEventHandler<TContent>>().HandleAsync(envelope, cancellationToken);
        return true;
    }

    private static void ValidateSegment(string segment, string name)
    {
        if (string.IsNullOrWhiteSpace(segment) || segment.Contains('.'))
        {
            throw new ArgumentException($"Route {name} must be a non-empty routing-key segment without dots.", name);
        }
    }
}
