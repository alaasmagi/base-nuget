using System.Text.Json;
using System.Text.Json.Serialization;
using Base.Message;
using Base.Keycloak.Events;
using Xunit;

public class ContractTests
{
    private const string UserId = "a57d2696-18b4-47ad-af5d-16b35b6a0201";
    private static readonly DateTimeOffset Expiry = new(2026, 7, 1, 15, 0, 0, TimeSpan.FromHours(3));
    private static BaseEventEnvelope<T> Envelope<T>(T content, string action = "user-created") =>
        BaseEventEnvelope<T>.Create("identity-hub", "app", action, "1.0", content);

    [Fact]
    public void EventOmitsExpiryAndMissingExpiryReadsAsNull()
    {
        var value = Envelope(new UserEventContent { UserId = UserId });
        var json = JsonSerializer.Serialize(value);
        using var document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.TryGetProperty("expiresAt", out _));
        Assert.False(document.RootElement.TryGetProperty("routingKey", out _));
        Assert.Null(JsonSerializer.Deserialize<BaseEventEnvelope<UserEventContent>>(json)!.ExpiresAt);
        Assert.True(Guid.TryParse(value.Id, out _));
        Assert.EndsWith("Z", value.Timestamp);
        Assert.NotEqual(value.Id, Envelope(new UserEventContent { UserId = UserId }).Id);
    }

    [Fact]
    public void CommandRetainsExplicitNullDespiteIgnoreNullOptions()
    {
        var value = BaseEventEnvelope<object>.CreateCommand("app", "app", "invoice", "1.0", new { }, null);
        var options = new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        var json = JsonSerializer.Serialize(value, options);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("expiresAt").ValueKind);
        var read = JsonSerializer.Deserialize<BaseEventEnvelope<object>>(json)!;
        Assert.Null(read.ExpiresAt);
        Assert.True(read.IsCommand);
        Assert.Contains("\"expiresAt\":null", JsonSerializer.Serialize(read));
    }

    [Fact]
    public void ArbitraryCommandSupportsExpiryAndNormalizesUtc()
    {
        var value = BaseEventEnvelope<object>.CreateCommand("app", "app", "custom", "1.0", new { }, Expiry);
        var json = JsonSerializer.Serialize(value);
        using var document = JsonDocument.Parse(json);
        Assert.Equal("2026-07-01T12:00:00Z", document.RootElement.GetProperty("expiresAt").GetString());
        Assert.Equal(Expiry, JsonSerializer.Deserialize<BaseEventEnvelope<object>>(json)!.ExpiresAt);
    }

    // Content fields per action, from "Identity commands" in docs/contracts/event-contract.md.
    private static readonly string[] NotificationFields = ["userId", "email", "fullName", "locale", "timeZone"];
    private static readonly Dictionary<string, string[]> ContractFields = new()
    {
        ["user-verify"] = [.. NotificationFields, "actionLink", "expiresAt", "validForMinutes"],
        ["user-2fa-otp"] = [.. NotificationFields, "otpCode", "expiresAt", "validForMinutes"],
        ["user-password-reset"] = [.. NotificationFields, "actionLink", "expiresAt", "validForMinutes"],
    };

    public static IEnumerable<object[]> Commands()
    {
        yield return new object[] { "user-verify", new VerifyEmailContent { UserId = UserId, Email = "user@example.com", FullName = "Test User", Locale = "et-EE", TimeZone = "Europe/Tallinn", ActionLink = "https://example.com/verify", ExpiresAt = Expiry, ValidForMinutes = 10 } };
        yield return new object[] { "user-password-reset", new PasswordResetContent { UserId = UserId, Email = "user@example.com", FullName = "Test User", Locale = "en-US", TimeZone = "Europe/Tallinn", ActionLink = "https://example.com/reset", ExpiresAt = Expiry, ValidForMinutes = 10 } };
        yield return new object[] { "user-2fa-otp", new OtpEmailContent { UserId = UserId, Email = "user@example.com", FullName = "Test User", Locale = "et-EE", TimeZone = "Europe/Tallinn", OtpCode = "123456", ExpiresAt = Expiry, ValidForMinutes = 5 } };
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
        Assert.Equal(document.RootElement.GetProperty("expiresAt").GetString(), actual.GetProperty("expiresAt").GetString());
        Assert.Equal(Expiry, JsonSerializer.Deserialize<BaseEventEnvelope<T>>(json)!.ExpiresAt);
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
    public async Task PublisherRejectsInvalidRecipientAndExpiryBeforeConnecting()
    {
        var publisher = new Base.Message.RabbitMQ.RabbitMqEventPublisher(null!, null!, null!);
        var invalid = new BaseNotificationContent { UserId = UserId, Email = "user@example.com", FullName = "Test User" };
        await Assert.ThrowsAsync<ArgumentException>(() => publisher.PublishAsync(Envelope(invalid, "invoice")));
        var valid = (OtpEmailContent)Commands().Last()[1];
        var mismatch = BaseEventEnvelope<OtpEmailContent>.CreateCommand("identity-hub", "app", "user-2fa-otp", "1.0", valid, null);
        await Assert.ThrowsAsync<ArgumentException>(() => publisher.PublishAsync(mismatch));
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
