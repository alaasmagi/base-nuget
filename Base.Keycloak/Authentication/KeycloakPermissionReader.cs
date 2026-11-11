using System.Text.Json;

namespace Base.Keycloak.Authentication;

/// <summary>
/// Reads Keycloak permissions from a <c>resource_access</c> claim value.
/// </summary>
internal static class KeycloakPermissionReader
{
    /// <summary>
    /// Gets the backend's own client identifier: <see cref="KeycloakOptions.ClientId"/>, or
    /// <see cref="KeycloakOptions.Audience"/> when no client identifier is set.
    /// </summary>
    internal static string? ResolveClientId(KeycloakOptions options)
    {
        return string.IsNullOrEmpty(options.ClientId) ? options.Audience : options.ClientId;
    }

    /// <summary>
    /// Returns the values of <c>resource_access.{clientId}.roles</c> that contain a dot. A malformed value yields
    /// no permissions.
    /// </summary>
    internal static List<string> Read(string resourceAccessJson, string clientId)
    {
        var permissions = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(resourceAccessJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty(clientId, out var clientElement)
                || clientElement.ValueKind != JsonValueKind.Object
                || !clientElement.TryGetProperty("roles", out var rolesElement)
                || rolesElement.ValueKind != JsonValueKind.Array)
            {
                return permissions;
            }

            foreach (var role in rolesElement.EnumerateArray())
            {
                if (role.ValueKind == JsonValueKind.String && role.GetString() is { } value && value.Contains('.'))
                {
                    permissions.Add(value);
                }
            }
        }
        catch (JsonException)
        {
            permissions.Clear();
        }

        return permissions;
    }
}
