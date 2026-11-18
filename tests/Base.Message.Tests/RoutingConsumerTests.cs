using System.Text.Json;
using Base.Contracts.Message;
using Base.Keycloak.Events;
using Base.Message;
using Base.Message.RabbitMQ;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public class RoutingConsumerTests
{
    private const string UserId = "a57d2696-18b4-47ad-af5d-16b35b6a0201";

    private readonly RecordingHandler<UserEventContent> _users = new();
    private readonly RecordingHandler<AiReply> _ai = new();
    private readonly RecordingHandler<AiSummary> _summaries = new();

    private ServiceProvider Services() => new ServiceCollection()
        .AddSingleton<IBaseEventHandler<UserEventContent>>(_users)
        .AddSingleton<IBaseEventHandler<AiReply>>(_ai)
        .AddSingleton<IBaseEventHandler<AiSummary>>(_summaries)
        .BuildServiceProvider();

    private static RabbitMqEventRouter Router() => new RabbitMqEventRouter()
        .Route<UserEventContent>("identity-hub")
        .Route<AiReply>("ai-hub")
        .Route<AiSummary>("ai-hub", "summary-ready");

    private static MessageDelivery Delivery<T>(string source, string tenant, string action, T content, string? routingKey = null)
    {
        var envelope = BaseEventEnvelope<T>.Create(source, tenant, action, "1.0", content);
        return new MessageDelivery
        {
            RoutingKey = routingKey ?? BaseRoutingKey.For(envelope),
            Body = JsonSerializer.SerializeToUtf8Bytes(envelope)
        };
    }

    private Task<MessageDisposition> RouteAsync(MessageDelivery delivery) =>
        Router().RouteAsync(delivery, Services(), NullLogger.Instance);

    [Fact]
    public async Task RoutesEachSourceToItsTypedHandler()
    {
        Assert.Equal(MessageDisposition.Ack, await RouteAsync(
            Delivery("identity-hub", "invoice-service", "user-updated", new UserEventContent { UserId = UserId })));
        Assert.Equal(MessageDisposition.Ack, await RouteAsync(
            Delivery("ai-hub", "invoice-service", "reply-ready", new AiReply { Text = "hello" })));

        Assert.Equal(UserId, Assert.Single(_users.Received).Content.UserId);
        Assert.Equal("hello", Assert.Single(_ai.Received).Content.Text);
        Assert.Empty(_summaries.Received);
    }

    [Fact]
    public async Task ActionSpecificRouteWinsOverSourceWideRoute()
    {
        Assert.Equal(MessageDisposition.Ack, await RouteAsync(
            Delivery("ai-hub", "invoice-service", "summary-ready", new AiSummary { Summary = "short" })));

        Assert.Equal("short", Assert.Single(_summaries.Received).Content.Summary);
        Assert.Empty(_ai.Received);
    }

    [Fact]
    public async Task RejectsDeliveryWithoutRoute()
    {
        Assert.Equal(MessageDisposition.Reject, await RouteAsync(
            Delivery("food-app", "invoice-service", "order-placed", new AiReply { Text = "x" })));

        Assert.Empty(_users.Received);
        Assert.Empty(_ai.Received);
    }

    [Theory]
    [InlineData("identity-hub.invoice-service")]
    [InlineData("identity-hub.invoice-service.user-updated.extra")]
    public async Task RejectsMalformedRoutingKey(string routingKey)
    {
        Assert.Equal(MessageDisposition.Reject, await RouteAsync(
            Delivery("identity-hub", "invoice-service", "user-updated", new UserEventContent { UserId = UserId }, routingKey)));

        Assert.Empty(_users.Received);
    }

    [Theory]
    [InlineData("identity-hub.food-app.user-updated")]
    [InlineData("identity-hub.invoice-service.user-deleted")]
    public async Task RejectsEnvelopeThatDoesNotMatchRoutingKey(string routingKey)
    {
        // Broker permissions apply to the routing key only, so a body claiming another tenant or action is rejected.
        Assert.Equal(MessageDisposition.Reject, await RouteAsync(
            Delivery("identity-hub", "invoice-service", "user-updated", new UserEventContent { UserId = UserId }, routingKey)));

        Assert.Empty(_users.Received);
    }

    [Fact]
    public async Task HandlerExceptionPropagatesSoTheConsumerAppliesItsExceptionDisposition()
    {
        var services = new ServiceCollection()
            .AddSingleton<IBaseEventHandler<UserEventContent>>(new ThrowingHandler<UserEventContent>())
            .BuildServiceProvider();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Router().RouteAsync(
            Delivery("identity-hub", "invoice-service", "user-updated", new UserEventContent { UserId = UserId }),
            services,
            NullLogger.Instance));
    }

    [Theory]
    [InlineData("")]
    [InlineData("ai.hub")]
    public void RouteRejectsInvalidSegments(string invalid)
    {
        Assert.Throws<ArgumentException>(() => new RabbitMqEventRouter().Route<AiReply>(invalid));
        Assert.Throws<ArgumentException>(() => new RabbitMqEventRouter().Route<AiReply>("ai-hub", invalid));
    }

    [Fact]
    public void RouteRejectsDuplicates()
    {
        var router = new RabbitMqEventRouter().Route<AiReply>("ai-hub");

        Assert.Throws<ArgumentException>(() => router.Route<AiSummary>("ai-hub"));
    }

    [Fact]
    public void AddRabbitMqRoutingConsumerRegistersOneHostedServicePerQueue()
    {
        var services = new ServiceCollection()
            .AddRabbitMqRoutingConsumer("invoice-service", routes => routes.Route<UserEventContent>("identity-hub"))
            .AddRabbitMqRoutingConsumer("other-service", routes => routes.Route<AiReply>("ai-hub"));

        Assert.Equal(2, services.Count(descriptor => descriptor.ServiceType == typeof(IHostedService)));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(RabbitMqConnectionManager));
    }

    public class AiReply
    {
        public string Text { get; init; } = "";
    }

    public class AiSummary
    {
        public string Summary { get; init; } = "";
    }

    private class RecordingHandler<T> : IBaseEventHandler<T>
    {
        public List<IBaseEventEnvelope<T>> Received { get; } = [];

        public Task HandleAsync(IBaseEventEnvelope<T> @event, CancellationToken cancellationToken)
        {
            Received.Add(@event);
            return Task.CompletedTask;
        }
    }

    private class ThrowingHandler<T> : IBaseEventHandler<T>
    {
        public Task HandleAsync(IBaseEventEnvelope<T> @event, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("handler failed");
    }
}
