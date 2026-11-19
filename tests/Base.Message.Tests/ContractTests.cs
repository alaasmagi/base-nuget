using System.Text.Json;
using Base.Message;
using Base.Keycloak.Events;
using Xunit;

public class ContractTests
{
    private const string UserId = "a57d2696-18b4-47ad-af5d-16b35b6a0201";
    private static readonly DateTimeOffset Expiry = new(2026, 7, 1, 15, 0, 0, TimeSpan.FromHours(3));
    private static BaseEventEnvelope<T> Envelope<T>(T content, string action = "user-created") =>
        BaseEventEnvelope<T>.Create("identity-hub", "app", action, "1.0", content);

    private static readonly string[] EnvelopeFields =
        ["id", "source", "tenant", "action", "timestamp", "contentVersion", "content"];

    private const string ValidJson = """
        {"content":{"userId":"a57d2696-18b4-47ad-af5d-16b35b6a0201"},"contentVersion":"1.0","timestamp":"2026-11-12T10:15:30.123Z",
         "action":"user-created","tenant":"app","source":"identity-hub","id":"3f2c9a1e-6a0e-4c4f-9f0e-2d8b6f1a0b11"}
        """;

    [Fact]
    public void EnvelopeHasExactlyTheContractFields()
    {
        var value = Envelope(new UserEventContent { UserId = UserId });
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(value));
        Assert.Equal(EnvelopeFields, document.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.True(Guid.TryParse(value.Id, out _));
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$", value.Timestamp);
        Assert.NotEqual(value.Id, Envelope(new UserEventContent { UserId = UserId }).Id);
    }

    [Fact]
    public void CommandEnvelopeHasNoExpiryEvenWhenContentHasOne()
    {
        var content = (OtpEmailContent)Commands().Last()[1];
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(Envelope(content, "email-user-2fa-otp")));
        Assert.Equal(EnvelopeFields, document.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal("2026-07-01T12:00:00Z", document.RootElement.GetProperty("content").GetProperty("expiresAt").GetString());
    }

    [Fact]
    public void ReadIgnoresFieldOrderAndUnknownFields()
    {
        var withExtra = ValidJson.Replace("\"id\"", "\"expiresAt\":null,\"id\"");
        var read = JsonSerializer.Deserialize<BaseEventEnvelope<UserEventContent>>(withExtra)!;
        Assert.Equal("identity-hub.app.user-created", BaseRoutingKey.For(read));
        Assert.Equal(UserId, read.Content.UserId);
    }

    [Theory]
    [InlineData("id")] [InlineData("source")] [InlineData("tenant")] [InlineData("action")]
    [InlineData("timestamp")] [InlineData("contentVersion")] [InlineData("content")]
    public void ReadRejectsMissingOrNullEnvelopeField(string field)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(ValidJson)!.AsObject();
        node[field] = null;
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<BaseEventEnvelope<UserEventContent>>(node.ToJsonString()));
        node.Remove(field);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<BaseEventEnvelope<UserEventContent>>(node.ToJsonString()));
    }

    [Fact]
    public void WriteRejectsNullContent()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(Envelope<object?>(null)));
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(Envelope(new { })));
        Assert.Equal(JsonValueKind.Object, document.RootElement.GetProperty("content").ValueKind);
    }

    // Content fields per action, from "Identity commands" in docs/contracts/event-contract.md.
    private static readonly string[] NotificationFields = ["userId", "email", "fullName", "locale", "timeZone"];
    private static readonly Dictionary<string, string[]> ContractFields = new()
    {
        ["email-user-verify"] = [.. NotificationFields, "actionLink", "expiresAt", "validForMinutes"],
        ["email-user-2fa-otp"] = [.. NotificationFields, "otpCode", "expiresAt", "validForMinutes"],
        ["email-user-password-reset"] = [.. NotificationFields, "actionLink", "expiresAt", "validForMinutes"],
    };

    public static IEnumerable<object[]> Commands()
    {
        yield return new object[] { "email-user-verify", new VerifyEmailContent { UserId = UserId, Email = "user@example.com", FullName = "Test User", Locale = "et-EE", TimeZone = "Europe/Tallinn", ActionLink = "https://example.com/verify", ExpiresAt = Expiry, ValidForMinutes = 10 } };
        yield return new object[] { "email-user-password-reset", new PasswordResetContent { UserId = UserId, Email = "user@example.com", FullName = "Test User", Locale = "en-US", TimeZone = "Europe/Tallinn", ActionLink = "https://example.com/reset", ExpiresAt = Expiry, ValidForMinutes = 10 } };
        yield return new object[] { "email-user-2fa-otp", new OtpEmailContent { UserId = UserId, Email = "user@example.com", FullName = "Test User", Locale = "et-EE", TimeZone = "Europe/Tallinn", OtpCode = "123456", ExpiresAt = Expiry, ValidForMinutes = 5 } };
    }

    [Theory]
    [MemberData(nameof(Commands))]
    public void CommandDtosMatchContract(string action, BaseNotificationContent content)
    {
        content.Validate();
        // Serialize using the concrete generic type, as callers and RabbitMQ do.
        var method = typeof(ContractTests).GetMethod(nameof(CheckCommand), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.MakeGenericMethod(content.GetType());
        method.Invoke(null, new object[] { action, content });
    }

    private static void CheckCommand<T>(string action, T content)
    {
        var value = Envelope(content, action);
        var json = JsonSerializer.Serialize(value);
        using var document = JsonDocument.Parse(json);
        var actual = document.RootElement.GetProperty("content");
        Assert.Equal(ContractFields[action].Order(), actual.EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal(UserId, actual.GetProperty("userId").GetString());
        Assert.Equal("Europe/Tallinn", actual.GetProperty("timeZone").GetString());
        Assert.Equal(JsonValueKind.Number, actual.GetProperty("validForMinutes").ValueKind);
        Assert.Equal("2026-07-01T12:00:00Z", actual.GetProperty("expiresAt").GetString());
        Assert.Equal(Expiry, ((IExpiringNotificationContent)JsonSerializer.Deserialize<BaseEventEnvelope<T>>(json)!.Content!).ExpiresAt);
    }

    [Fact]
    public void UserEventsCarryTheContractFields()
    {
        var content = new UserEventContent { UserId = UserId, Email = "user@example.com", Username = "user", FullName = "Some User", Locale = "et-EE", TimeZone = "Europe/Tallinn" };
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(Envelope(content)));
        Assert.Equal(new[] { "email", "fullName", "locale", "timeZone", "userId", "username" },
            document.RootElement.GetProperty("content").EnumerateObject().Select(p => p.Name).Order());
    }

    [Fact]
    public void DeletedUserContainsOnlyUserId()
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(Envelope(new UserEventContent { UserId = UserId }, "user-deleted")));
        Assert.Equal(new[] { "userId" }, document.RootElement.GetProperty("content").EnumerateObject().Select(p => p.Name));
        Assert.False(typeof(BaseNotificationContent).IsAssignableFrom(typeof(UserEventContent)));
    }

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData(" ")] [InlineData("bad.segment")]
    public void RoutingRejectsInvalidValuesInEveryPosition(string? invalid)
    {
        foreach (var position in Enumerable.Range(0, 3))
        {
            var parts = new[] { "identity-hub", "app", "user-created" };
            parts[position] = invalid!;
            var value = BaseEventEnvelope<object>.Create(parts[0], parts[1], parts[2], "1.0", new { });
            Assert.Throws<ArgumentException>(() => BaseRoutingKey.For(value));
        }
    }

    [Fact]
    public void RoutingUsesEnvelopeFieldsAndRejectsNull()
    {
        Assert.Equal("identity-hub.app.user-created", BaseRoutingKey.For(Envelope(new { })));
        Assert.Throws<ArgumentNullException>(() => BaseRoutingKey.For<object>(null!));
        Assert.Equal("notification-hub", DefaultSources.NotificationHub);
        Assert.Equal("identity-hub", DefaultSources.IdentityHub);
    }

    [Fact]
    public async Task PublisherRejectsInvalidRecipientAndNullFieldsBeforeConnecting()
    {
        var publisher = new Base.Message.RabbitMQ.RabbitMqEventPublisher(null!, null!, null!);
        var invalid = new BaseNotificationContent { UserId = UserId, Email = "user@example.com", FullName = "Test User" };
        await Assert.ThrowsAsync<ArgumentException>(() => publisher.PublishAsync(Envelope(invalid, "email-invoice")));
        await Assert.ThrowsAsync<ArgumentException>(() => publisher.PublishAsync(Envelope<object?>(null)));
        var nullVersion = new BaseEventEnvelope<object>
        {
            Id = Guid.NewGuid().ToString(), Source = "identity-hub", Tenant = "app", Action = "user-created",
            Timestamp = BaseEventEnvelope<object>.FormatTimestamp(DateTimeOffset.UtcNow), ContentVersion = null!,
            Content = new { }
        };
        await Assert.ThrowsAsync<ArgumentException>(() => publisher.PublishAsync(nullVersion));
    }

    [Theory]
    [InlineData("et", "Europe/Tallinn")]
    [InlineData(null, "Europe/Tallinn")]
    [InlineData("et-EE", null)]
    [InlineData("et-EE", "not/a-zone")]
    public void InvalidRecipientMetadataIsRejected(string? locale, string? zone)
    {
        var content = new BaseNotificationContent { UserId = UserId, Email = "user@example.com", FullName = "Test User", Locale = locale, TimeZone = zone };
        Assert.Throws<ArgumentException>(() => content.Validate());
    }
}
