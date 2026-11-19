using System.Diagnostics;
using System.Reflection;
using Base.Contracts.Message;
using Base.Keycloak.Events;
using Base.Message;
using Base.Message.RabbitMQ;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;

public class DeliveryConsumerRequeueTests
{
    private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(200);

    [Fact]
    public async Task RequeueWaitsRequeueDelayBeforeNackWithRequeue()
    {
        var consumer = new TestConsumer(MessageDisposition.Requeue, Delay);
        var channel = RecordingChannel.Create();

        var stopwatch = Stopwatch.StartNew();
        await consumer.OnReceivedAsync(channel, Delivery(7), CancellationToken.None);
        stopwatch.Stop();

        var call = Assert.Single(((RecordingChannel)(object)channel).Calls);
        Assert.Equal(("BasicNackAsync", 7ul, true), call);
        Assert.True(stopwatch.Elapsed >= Delay - TimeSpan.FromMilliseconds(20),
            $"Nacked after {stopwatch.Elapsed}, expected at least {Delay}.");
    }

    [Fact]
    public async Task ExceptionDispositionRequeueAlsoWaits()
    {
        var consumer = new TestConsumer(null, Delay, MessageDisposition.Requeue);
        var channel = RecordingChannel.Create();

        var stopwatch = Stopwatch.StartNew();
        await consumer.OnReceivedAsync(channel, Delivery(3), CancellationToken.None);
        stopwatch.Stop();

        Assert.Equal(("BasicNackAsync", 3ul, true), Assert.Single(((RecordingChannel)(object)channel).Calls));
        Assert.True(stopwatch.Elapsed >= Delay - TimeSpan.FromMilliseconds(20));
    }

    [Fact]
    public async Task RequeueDoesNotThrowOrNackWhenStoppingDuringDelay()
    {
        var consumer = new TestConsumer(MessageDisposition.Requeue, TimeSpan.FromMinutes(5));
        var channel = RecordingChannel.Create();
        using var stopping = new CancellationTokenSource();

        var handling = consumer.OnReceivedAsync(channel, Delivery(9), stopping.Token);
        await stopping.CancelAsync();
        await handling.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Empty(((RecordingChannel)(object)channel).Calls);
    }

    [Theory]
    [InlineData(MessageDisposition.Ack, "BasicAckAsync", false)]
    [InlineData(MessageDisposition.Reject, "BasicNackAsync", false)]
    public async Task AckAndRejectDoNotWait(MessageDisposition disposition, string method, bool requeue)
    {
        var consumer = new TestConsumer(disposition, TimeSpan.FromMinutes(5));
        var channel = RecordingChannel.Create();

        await consumer.OnReceivedAsync(channel, Delivery(1), CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal((method, 1ul, requeue), Assert.Single(((RecordingChannel)(object)channel).Calls));
    }

    private static BasicDeliverEventArgs Delivery(ulong deliveryTag) =>
        new("consumer", deliveryTag, false, "notification-hub.commands", "identity-hub.educode.email-user-verify",
            new BasicProperties(), ReadOnlyMemory<byte>.Empty);

    private sealed class TestConsumer(
        MessageDisposition? disposition,
        TimeSpan requeueDelay,
        MessageDisposition exceptionDisposition = MessageDisposition.Reject)
        : RabbitMqDeliveryConsumerBase(null!, NullLogger.Instance, "test-queue")
    {
        protected override TimeSpan RequeueDelay => requeueDelay;

        protected override MessageDisposition ExceptionDisposition => exceptionDisposition;

        protected override Task<MessageDisposition> HandleDeliveryAsync(
            MessageDelivery delivery,
            CancellationToken cancellationToken) =>
            disposition is { } value
                ? Task.FromResult(value)
                : throw new InvalidOperationException("Transient failure.");
    }

    public class RecordingChannel : DispatchProxy
    {
        public List<(string Method, ulong DeliveryTag, bool Requeue)> Calls { get; } = [];

        public static IChannel Create() => Create<IChannel, RecordingChannel>();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod?.Name)
            {
                case nameof(IChannel.BasicAckAsync):
                    Calls.Add((targetMethod.Name, (ulong)args![0]!, false));
                    return ValueTask.CompletedTask;
                case nameof(IChannel.BasicNackAsync):
                    Calls.Add((targetMethod.Name, (ulong)args![0]!, (bool)args[2]!));
                    return ValueTask.CompletedTask;
                default:
                    throw new NotSupportedException(targetMethod?.Name);
            }
        }
    }
}

public class TypedConsumerTests
{
    private const string UserId = "a57d2696-18b4-47ad-af5d-16b35b6a0201";

    private static MessageDelivery Delivery(string body) => new()
    {
        RoutingKey = "identity-hub.app.user-created",
        Body = System.Text.Encoding.UTF8.GetBytes(body)
    };

    private static MessageDelivery Valid() => new()
    {
        RoutingKey = "identity-hub.app.user-created",
        Body = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(BaseEventEnvelope<UserEventContent>.Create(
            "identity-hub", "app", "user-created", "1.0", new UserEventContent { UserId = UserId }))
    };

    [Theory]
    [InlineData("null")]
    [InlineData("not json")]
    [InlineData("""{"id":"x","source":"identity-hub","tenant":"app","action":"user-created","timestamp":"t","contentVersion":"1.0","content":null}""")]
    [InlineData("""{"id":"x","source":"identity-hub","tenant":"app","action":"user-created","timestamp":"t","content":{}}""")]
    public async Task InvalidEnvelopeIsRejectedWithoutReachingTheHandler(string body)
    {
        var typed = new TypedConsumer(MessageDisposition.Requeue);
        var plain = new PlainConsumer();

        Assert.Equal(MessageDisposition.Reject, await typed.Handle(Delivery(body)));
        Assert.Equal(MessageDisposition.Reject, await plain.Handle(Delivery(body)));
        Assert.Equal(0, typed.Calls);
        Assert.Empty(plain.Handler.Received);
    }

    [Fact]
    public async Task TypedConsumerReturnsTheSubclassDisposition()
    {
        var typed = new TypedConsumer(MessageDisposition.Requeue);
        Assert.Equal(MessageDisposition.Requeue, await typed.Handle(Valid()));
        Assert.Equal(1, typed.Calls);
    }

    [Fact]
    public async Task ConsumerBaseAcksAfterHandlerAndAlwaysRejectsFailures()
    {
        var plain = new PlainConsumer();
        Assert.Equal(MessageDisposition.Ack, await plain.Handle(Valid()));
        Assert.Equal(UserId, Assert.Single(plain.Handler.Received).Content.UserId);
        Assert.Equal(MessageDisposition.Reject, plain.Exception);
    }

    private sealed class TypedConsumer(MessageDisposition disposition)
        : RabbitMqDeliveryConsumerBase<UserEventContent>(null!, NullLogger.Instance, "test-queue")
    {
        public int Calls { get; private set; }

        public Task<MessageDisposition> Handle(MessageDelivery delivery) =>
            HandleDeliveryAsync(delivery, CancellationToken.None);

        protected override Task<MessageDisposition> HandleAsync(
            BaseEventEnvelope<UserEventContent> envelope,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(disposition);
        }
    }

    private sealed class PlainConsumer : RabbitMqConsumerBase<UserEventContent>
    {
        public PlainConsumer() : this(new Recording())
        {
        }

        private PlainConsumer(Recording handler) : base(null!, handler, NullLogger.Instance, "test-queue")
        {
            Handler = handler;
        }

        public Recording Handler { get; }

        public MessageDisposition Exception => ExceptionDisposition;

        public Task<MessageDisposition> Handle(MessageDelivery delivery) =>
            HandleDeliveryAsync(delivery, CancellationToken.None);
    }

    private sealed class Recording : IBaseEventHandler<UserEventContent>
    {
        public List<IBaseEventEnvelope<UserEventContent>> Received { get; } = [];

        public Task HandleAsync(IBaseEventEnvelope<UserEventContent> @event, CancellationToken cancellationToken = default)
        {
            Received.Add(@event);
            return Task.CompletedTask;
        }
    }
}
