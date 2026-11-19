using System.Text.Json;
using System.Text.Json.Serialization;

namespace Base.Message;

/// <summary>
/// Reads and writes exactly the envelope fields of the platform event contract. Reading fails with
/// <see cref="JsonException"/> when any envelope field is missing or <c>null</c>, or <c>content</c> is not an object;
/// unknown fields are ignored. Writing fails the same way instead of putting a <c>null</c> field on the wire.
/// </summary>
public sealed class BaseEventEnvelopeJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(BaseEventEnvelope<>);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(typeof(EnvelopeConverter<>).MakeGenericType(
            typeToConvert.GetGenericArguments()))!;

    private sealed class EnvelopeConverter<TContent> : JsonConverter<BaseEventEnvelope<TContent>>
    {
        public override BaseEventEnvelope<TContent> Read(ref Utf8JsonReader reader, Type typeToConvert,
            JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new JsonException("The envelope must be a JSON object.");
            string Required(string name) => root.TryGetProperty(name, out var value) &&
                value.ValueKind == JsonValueKind.String ? value.GetString()! :
                throw new JsonException($"Missing or invalid envelope field '{name}'.");
            if (!root.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Object)
                throw new JsonException("Missing or invalid envelope field 'content'.");
            return new BaseEventEnvelope<TContent>
            {
                Id = Required("id"), Source = Required("source"), Tenant = Required("tenant"),
                Action = Required("action"), Timestamp = Required("timestamp"),
                ContentVersion = Required("contentVersion"),
                Content = content.Deserialize<TContent>(options)
                          ?? throw new JsonException("Missing or invalid envelope field 'content'.")
            };
        }

        public override void Write(Utf8JsonWriter writer, BaseEventEnvelope<TContent> value,
            JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString("id", NotNull(value.Id, "id"));
            writer.WriteString("source", NotNull(value.Source, "source"));
            writer.WriteString("tenant", NotNull(value.Tenant, "tenant"));
            writer.WriteString("action", NotNull(value.Action, "action"));
            writer.WriteString("timestamp", NotNull(value.Timestamp, "timestamp"));
            writer.WriteString("contentVersion", NotNull(value.ContentVersion, "contentVersion"));
            writer.WritePropertyName("content");
            JsonSerializer.Serialize(writer, NotNull(value.Content, "content"), options);
            writer.WriteEndObject();
        }

        private static T NotNull<T>(T? value, string name) =>
            value ?? throw new JsonException($"Envelope field '{name}' must not be null.");
    }
}
