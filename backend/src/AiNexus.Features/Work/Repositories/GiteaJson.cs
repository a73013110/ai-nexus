using System.Globalization;
using System.Text.Json;

namespace AiNexus.Features.Work.Repositories;

/// <summary>有界限、寬容地讀取 Gitea JSON：欄位缺失或型別不符時回傳空值，不丟例外。</summary>
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

    /// <summary>使用者物件的 login；沒有 login 時退回 full_name。</summary>
    public static string Login(JsonElement element, string key)
    {
        var user = Child(element, key);
        var login = Text(user, "login", 100);
        return login.Length > 0 ? login : Text(user, "full_name", 100);
    }

    public static IReadOnlyList<RepositoryLabelDto> Labels(JsonElement element) => Items(element, "labels").Take(10)
        .Select(x => new RepositoryLabelDto(Text(x, "name", 60), Color(Text(x, "color", 9)))).Where(x => x.Name.Length > 0).ToArray();

    /// <summary>Gitea 標籤顏色格式為 #rrggbb；其他格式直接捨棄，避免外部字串被原樣放進樣式。</summary>
    private static string Color(string value)
    {
        var hex = value.TrimStart('#');
        return hex.Length == 6 && hex.All(char.IsAsciiHexDigit) ? "#" + hex.ToLowerInvariant() : "";
    }
}
