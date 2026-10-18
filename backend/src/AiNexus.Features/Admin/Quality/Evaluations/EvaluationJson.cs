using System.Text.Json;

namespace AiNexus.Features.Admin.Quality.Evaluations;

/// <summary>題組與執行凍結的 JSON 欄位；採預設序列化選項，須與寫入端一致，否則既有資料讀不回來。</summary>
internal static class EvaluationJson
{
    public static T[] Parse<T>(string json) => JsonSerializer.Deserialize<T[]>(json)!;
}
