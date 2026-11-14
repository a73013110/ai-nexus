using System.Text.Json;

namespace AiNexus.Features.Office.Flow.Rules;

/// <summary>審核人規則參數的 JSON 讀取；讀不懂（格式錯、少欄位）一律回 null，由規則決定是驗證失敗還是算不出人。</summary>
internal static class ApproverArgument
{
    public static T? Read<T>(string? json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<T>(json, JsonSerializerOptions.Web); }
        catch (JsonException) { return null; }
    }

    /// <summary><see cref="ApproverArguments.Users"/> 形式的人員 ID；讀不懂回 null。</summary>
    public static Guid[]? UserIds(string? json) => Read<UsersArgument>(json)?.UserIds;

    private sealed record UsersArgument(Guid[]? UserIds);
}
