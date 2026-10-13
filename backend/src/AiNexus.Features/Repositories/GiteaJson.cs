using System.Globalization;
using System.Text.Json;

namespace AiNexus.Features.Repositories;

/// <summary>Bounded, tolerant reads of Gitea JSON: a missing or mistyped field becomes empty, never an exception.</summary>
internal static class GiteaJson
{
    public static string Text(JsonElement element, string key, int max) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
            ? string.Concat((value.GetString() ?? "").Take(max)) : "";

    public static long Number(JsonElement element, string key) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number) ? number : 0;

    public static int Int(JsonElement element, string key) => (int)Math.Clamp(Number(element, key), int.MinValue, int.MaxValue);

    public static bool Flag(JsonElement element, string key) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.True;

    public static DateTimeOffset? Date(JsonElement element, string key) =>
        DateTimeOffset.TryParse(Text(element, key, 64), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at) ? at.ToUniversalTime() : null;

    public static JsonElement Child(JsonElement element, string key) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Object ? value : default;

    public static IEnumerable<JsonElement> Items(JsonElement element, string? key = null)
    {
        var list = key is null ? element : element.ValueKind == JsonValueKind.Object && element.TryGetProperty(key, out var value) ? value : default;
        return list.ValueKind == JsonValueKind.Array ? list.EnumerateArray() : [];
    }

    /// <summary>A user object's login, falling back to its full name.</summary>
    public static string Login(JsonElement element, string key)
    {
        var user = Child(element, key);
        var login = Text(user, "login", 100);
        return login.Length > 0 ? login : Text(user, "full_name", 100);
    }

    public static IReadOnlyList<RepositoryLabelDto> Labels(JsonElement element) => Items(element, "labels").Take(10)
        .Select(x => new RepositoryLabelDto(Text(x, "name", 60), Color(Text(x, "color", 9)))).Where(x => x.Name.Length > 0).ToArray();

    /// <summary>Gitea label colours as #rrggbb; anything else is dropped rather than echoed into styles.</summary>
    private static string Color(string value)
    {
        var hex = value.TrimStart('#');
        return hex.Length == 6 && hex.All(char.IsAsciiHexDigit) ? "#" + hex.ToLowerInvariant() : "";
    }
}
