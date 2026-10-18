using AiNexus.Data.App;
using AiNexus.Data.App.Inference;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Platform.Inference.Runs;

/// <summary>
/// 生成耗時與 Token 統計資料傳輸物件。
/// </summary>
public sealed record RunTimingDto(
    long TotalMilliseconds,
    long QueueMilliseconds,
    long? GenerationMilliseconds,
    long? InputTokens,
    long? OutputTokens);

/// <summary>
/// 生成耗時計算與元資料查詢工具。
/// 提供輕量化查詢，只讀取耗時與 Token 欄位，不加載龐大的回答內容文字或生成參數 JSON。
/// </summary>
public static class RunTiming
{
    /// <summary>
    /// 批次讀取多筆生成的耗時指標元資料（不加載訊息文字內容，降低記憶體開銷）。
    /// </summary>
    public static async Task<Dictionary<Guid, RunTimingDto>> ReadAsync(
        AppDbContext db,
        IReadOnlyList<Guid> ids,
        CancellationToken ct)
    {
        if (ids.Count == 0) return [];

        return await db.Runs.AsNoTracking()
            .Where(x => ids.Contains(x.Id) && x.DurationMilliseconds != null)
            .Select(x => new GenerationRun
            {
                Id = x.Id,
                DurationMilliseconds = x.DurationMilliseconds,
                GenerationMilliseconds = x.GenerationMilliseconds,
                InputTokens = x.InputTokens,
                OutputTokens = x.OutputTokens
            })
            .ToDictionaryAsync(x => x.Id, x => Describe(x)!, ct);
    }

    /// <summary>
    /// 記錄生成結束時間並結算總耗時與純推論生成耗時。
    /// </summary>
    public static void Finish(GenerationRun run, DateTimeOffset now)
    {
        run.FinishedAt = now;
        run.DurationMilliseconds = Milliseconds(run.CreatedAt, now);
        run.GenerationMilliseconds = run.StartedAt is { } started
            ? Math.Min(run.DurationMilliseconds.Value, Milliseconds(started, now))
            : null;
    }

    /// <summary>
    /// 將實體的耗時資料轉換為 DTO，並計算排隊等待毫秒數。
    /// </summary>
    public static RunTimingDto? Describe(GenerationRun run) => run.DurationMilliseconds is { } total
        ? new(total, Math.Max(0, total - (run.GenerationMilliseconds ?? 0)), run.GenerationMilliseconds, run.InputTokens, run.OutputTokens)
        : null;

    /// <summary>計算兩個時間點之間的毫秒差值（最小為 0）。</summary>
    public static long Milliseconds(DateTimeOffset start, DateTimeOffset end) =>
        Math.Max(0, (long)(end - start).TotalMilliseconds);
}
