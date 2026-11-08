using AiNexus.Data.App;
using AiNexus.Data.App.CalendarSync;
using AiNexus.Platform.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Office.CalendarSync.Sync;

/// <summary>手動同步的請求：操作者與是否確認被安全閥擋下的移除。</summary>
public sealed record CalendarSyncRequest(Guid RequestedBy, bool ConfirmRemovals);

/// <summary>一輪的結果；<see cref="Run"/> 是寫入的執行紀錄（沒有異動的排程輪不寫，為 null）。</summary>
public sealed record CalendarSyncOutcome(string Status, string? ErrorCode, int Created, int Updated, int Deleted, int Failed, int Held, SyncRun? Run);

/// <summary>
/// 執行一輪同步：讀 GD → 比對（<see cref="CalendarSyncPlanner"/>）→ 寫入待同步 → 推到 Google（<see cref="CalendarEventPusher"/>）→ 記錄。
/// <list type="bullet">
/// <item><b>單一執行者</b>：由排程任務（<see cref="CalendarSyncTask"/>）的租約保證，開發機與正式站共用資料庫也不會同時同步。</item>
/// <item><b>GD 不可用不刪除</b>：讀不到 GD 時不規劃（空清單與讀不到不同），只把先前排定的推送做完。</item>
/// <item><b>推送平行、寫回分批</b>：每批同時送出 <see cref="CalendarSyncOptions.MaxParallelism"/> 個請求，結果一批寫回一次；
/// 中途程序結束，已推送卻沒寫回的事件下一輪再推一次，推送是冪等的所以不會重複。</item>
/// <item><b>整輪停止</b>：憑證、權限或日曆本身有問題時停下，不讓每一列都記一次失敗與退避，修好後手動同步立即補上。</item>
/// <item><b>重設</b>（<see cref="ResetAsync"/>）走同一條推送路徑刪除事件，所以冪等、平行與限速的處理都相同。</item>
/// </list>
/// </summary>
[AiNexus.Platform.Modules.Scoped]
public sealed partial class CalendarSyncRunner(
    AppDbContext db,
    GdAbsenceReader source,
    CalendarEventPusher pusher,
    AuditTrail audit,
    IOptions<CalendarSyncOptions> options,
    TimeProvider clock,
    ILogger<CalendarSyncRunner> logger)
{
    public async Task<CalendarSyncOutcome> RunAsync(CalendarSyncRequest? request, CancellationToken ct)
    {
        var settings = options.Value;
        var run = new Tally(clock.GetUtcNow());
        try
        {
            await PlanAsync(settings, request?.ConfirmRemovals == true, run, ct);
            await PushAsync(settings, manual: request is not null, run, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // 資料庫中斷、程式錯誤：已寫回的推送結果保留，這一輪記為失敗，下一輪從待同步的列接著做。
            // 沒寫進去的變更要丟掉，否則寫執行紀錄時會連同它們再存一次、以同樣的原因失敗，這一輪就沒有紀錄。
            db.ChangeTracker.Clear();
            run.Fail(CalendarSyncFailures.Unexpected, ex.GetType().Name);
        }
        return await FinishAsync(settings, request, run, ct);
    }

    /// <summary>重設時連續這麼多次沒有任何進展就放棄，留給下一次重設。</summary>
    private const int MaxStalledPasses = 5;

    /// <summary>
    /// 重設本站設定的日曆：把這個日曆的事件列全部排成移除、照一般推送刪除 Google 上的事件（不存在的當成已刪），
    /// 全部刪完才清掉事件列與同步紀錄；只刪資料庫有紀錄的事件，手動在 Google 建立的不動。
    /// 被限速時暫停（<see cref="CalendarSyncOptions.RateLimitPauseSeconds"/>，Google 要求更久時聽它的）後繼續，直到任務逾時；刪不掉（憑證、權限、連續無進展）時保留列並記為失敗，畫面顯示原因，再按一次重設會接著刪。
    /// 中途停下時，還在公文系統的事件下一輪同步會救回，不會留下列與日曆不一致的狀態。
    /// </summary>
    public async Task<CalendarSyncOutcome> ResetAsync(CalendarSyncRequest request, CancellationToken ct)
    {
        var settings = options.Value;
        var calendarId = settings.CalendarId;
        var run = new Tally(clock.GetUtcNow());
        try
        {
            var now = clock.GetUtcNow();
            await db.CalendarEvents.Where(x => x.CalendarId == calendarId && x.State != CalendarEventStates.Removed)
                .ExecuteUpdateAsync(p => p
                    .SetProperty(x => x.RemovedAt, x => x.RemovedAt ?? now)
                    .SetProperty(x => x.State, CalendarEventStates.Pending)
                    .SetProperty(x => x.Attempts, 0)
                    .SetProperty(x => x.NextAttemptAt, (DateTimeOffset?)null)
                    .SetProperty(x => x.LastErrorCode, (string?)null)
                    .SetProperty(x => x.LastError, (string?)null)
                    .SetProperty(x => x.UpdatedAt, now), ct);

            var stalled = 0;
            while (true)
            {
                var deleted = run.Deleted;
                var limit = await PushAsync(settings, manual: true, run, ct);
                // 每輪都寫回過了；清掉追蹤，大量事件時 SaveChanges 才不會一輪比一輪慢。
                db.ChangeTracker.Clear();
                if (run.ErrorCode is not null) break;
                if (!await db.CalendarEvents.AnyAsync(x => x.CalendarId == calendarId && x.State != CalendarEventStates.Removed, ct))
                {
                    var events = await Batches.RepeatAsync(() => db.CalendarEvents.Where(x => x.CalendarId == calendarId).Take(Batches.Size).ExecuteDeleteAsync(ct));
                    var runs = await Batches.RepeatAsync(() => db.SyncRuns.Where(x => x.CalendarId == calendarId).Take(Batches.Size).ExecuteDeleteAsync(ct));
                    audit.Record(CalendarSyncAudit.Cleared, request.RequestedBy, details: new { calendarId, deleted = run.Deleted, events, runs });
                    await db.SaveChangesAsync(CancellationToken.None);
                    LogReset(logger, run.Deleted, events, runs);
                    return new(SyncRunStatuses.Succeeded, null, 0, 0, run.Deleted, 0, 0, null);
                }
                stalled = run.Deleted > deleted ? 0 : stalled + 1;
                if (limit is { } wait && stalled < MaxStalledPasses)
                {
                    var pause = TimeSpan.FromSeconds(settings.RateLimitPauseSeconds);
                    await Task.Delay(wait > pause ? wait : pause, clock, ct);
                }
                else if (stalled > 0)
                {
                    // 沒被限速卻一筆都刪不掉（Google 拒絕或暫時不可用），或限速一直不解除：以最近一筆的原因結束。
                    var reason = await db.CalendarEvents.AsNoTracking()
                        .Where(x => x.CalendarId == calendarId && x.State == CalendarEventStates.Failed && x.LastErrorCode != null)
                        .OrderByDescending(x => x.LastAttemptAt).Select(x => x.LastErrorCode).FirstOrDefaultAsync(ct);
                    run.Fail(reason ?? CalendarSyncFailures.Unexpected, "Reset stopped before every event was removed.");
                    break;
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            db.ChangeTracker.Clear();
            run.Fail(CalendarSyncFailures.Unexpected, ex.GetType().Name);
        }
        return await FinishAsync(settings, request, run, ct);
    }

    private async Task PlanAsync(CalendarSyncOptions settings, bool confirmRemovals, Tally run, CancellationToken ct)
    {
        var calendarId = settings.CalendarId;
        var windowStart = clock.GetUtcNow().AddDays(-settings.LookbackDays);
        var known = await db.CalendarEvents.AsNoTracking()
            .Where(x => x.CalendarId == calendarId && x.State != CalendarEventStates.Removed && x.EndsAt >= windowStart)
            .Select(x => new KnownEvent(x.Id, x.DesNo, x.SegmentKey, x.ContentHash, x.State, true, x.RemovedAt != null)).ToListAsync(ct);

        var desired = await source.ReadWindowAsync(windowStart, ct);
        if (desired is not null)
        {
            // 窗內的事件在 GD 的窗內查不到時，單可能只是改到窗外的日期，再以文號確認一次，免得把仍有效的單刪掉。
            var seen = desired.Select(x => x.DesNo).ToHashSet(StringComparer.Ordinal);
            var missing = known.Select(x => x.DesNo).Where(x => !seen.Contains(x)).Distinct(StringComparer.Ordinal).ToList();
            var moved = await source.ReadByDesNoAsync(missing, ct);
            desired = moved is null ? null : [.. desired, .. moved];
        }
        if (desired is null)
        {
            run.Fail(CalendarSyncFailures.GdUnavailable, "GD is unavailable; nothing was planned or removed.");
            return;
        }
        run.SourceCount = desired.Count;

        // GD 有、窗內卻沒有的段：可能是已移除（救回）或結束時間在窗外的舊列，以文號找出來，沒有才是新的。
        var knownKeys = known.Select(x => (x.DesNo, x.SegmentKey)).ToHashSet();
        var others = desired.Where(x => !knownKeys.Contains(x.Key)).Select(x => x.DesNo).Distinct(StringComparer.Ordinal).ToList();
        foreach (var chunk in others.Chunk(1000))
        {
            known.AddRange(await db.CalendarEvents.AsNoTracking()
                .Where(x => x.CalendarId == calendarId && chunk.Contains(x.DesNo))
                .Select(x => new KnownEvent(x.Id, x.DesNo, x.SegmentKey, x.ContentHash, x.State, false, x.RemovedAt != null)).ToListAsync(ct));
        }
        known = [.. known.DistinctBy(x => x.Id)];

        var plan = CalendarSyncPlanner.Plan(desired, known, settings.RemovalHoldRatio, settings.RemovalHoldMinimum, confirmRemovals);
        if (plan.HeldRemovals > 0) run.Hold(plan.HeldRemovals);
        if (plan.Total > 0) await ApplyAsync(calendarId, plan, ct);
    }

    /// <summary>把規劃寫成待同步（一個交易）；推送在之後讀待同步的列，所以中途失敗的一輪留下的工作，下一輪照樣會做。</summary>
    private async Task ApplyAsync(string calendarId, CalendarSyncPlan plan, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        foreach (var creation in plan.Creations)
        {
            var row = new CalendarEvent { CalendarId = calendarId, DesNo = creation.Segment.DesNo, SegmentKey = creation.Segment.SegmentKey, CreatedAt = now };
            row.GoogleEventId = CalendarEvent.NewGoogleEventId(row.Id);
            Describe(row, creation.Segment, creation.ContentHash, now);
            db.CalendarEvents.Add(row);
        }

        var ids = plan.Changes.Select(x => x.Id).Concat(plan.Removals).ToList();
        var rows = new Dictionary<Guid, CalendarEvent>();
        foreach (var chunk in ids.Chunk(1000))
            foreach (var row in await db.CalendarEvents.Where(x => chunk.Contains(x.Id)).ToListAsync(ct)) rows[row.Id] = row;
        foreach (var change in plan.Changes)
        {
            var row = rows[change.Id];
            row.RemovedAt = null;
            Describe(row, change.Segment, change.ContentHash, now);
        }
        foreach (var id in plan.Removals)
        {
            var row = rows[id];
            row.RemovedAt = now;
            Queue(row, now);
        }
        await db.SaveChangesAsync(ct);
    }

    private static void Describe(CalendarEvent row, AbsenceSegment segment, byte[] hash, DateTimeOffset now)
    {
        row.Kind = segment.Kind;
        row.ApplicantName = segment.ApplicantName;
        row.AgentName = segment.AgentName;
        row.FlowStatus = segment.FlowStatus;
        row.StartsAt = segment.StartsAt;
        row.EndsAt = segment.EndsAt;
        row.ContentHash = hash;
        Queue(row, now);
    }

    /// <summary>內容或去留變了：重新排進推送，失敗次數歸零（新的內容值得馬上再試一次）。</summary>
    private static void Queue(CalendarEvent row, DateTimeOffset now)
    {
        row.State = CalendarEventStates.Pending;
        row.Attempts = 0;
        row.NextAttemptAt = null;
        row.LastErrorCode = null;
        row.LastError = null;
        row.UpdatedAt = now;
    }

    /// <summary>推送到期的列。被 Google 限速而提前停下時，回傳對方要求的等待（沒說就是 0），剩下的留到下一輪；其餘回傳 null。</summary>
    private async Task<TimeSpan?> PushAsync(CalendarSyncOptions settings, bool manual, Tally run, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var calendarId = settings.CalendarId;
        // 手動同步不等退避時間：管理員修好問題後按下去就要立刻補上。
        var due = await db.CalendarEvents
            .Where(x => x.CalendarId == calendarId && (x.State == CalendarEventStates.Pending || x.State == CalendarEventStates.Failed)
                && (manual || x.NextAttemptAt == null || x.NextAttemptAt <= now))
            .OrderBy(x => x.NextAttemptAt).ThenBy(x => x.UpdatedAt)
            .Take(settings.MaxPushesPerRun).ToListAsync(ct);

        foreach (var batch in due.Chunk(settings.MaxParallelism * 5))
        {
            var outcomes = new PushOutcome[batch.Length];
            await Parallel.ForEachAsync(Enumerable.Range(0, batch.Length), new ParallelOptions { MaxDegreeOfParallelism = settings.MaxParallelism, CancellationToken = ct },
                async (i, token) => outcomes[i] = await pusher.PushAsync(ItemOf(batch[i]), token));

            var stop = outcomes.FirstOrDefault(x => x.StopsRun);
            var at = clock.GetUtcNow();
            for (var i = 0; i < batch.Length; i++)
            {
                // 整輪停止的原因與這一列無關，不記在列上、不累計退避；其他已完成的結果照常寫回。
                if (outcomes[i].StopsRun) continue;
                Record(batch[i], outcomes[i], at, settings, run);
            }
            await db.SaveChangesAsync(ct);
            if (stop is not null)
            {
                run.Fail(CalendarSyncFailures.Of(stop.Status), stop.Message);
                return null;
            }
            // 被限速時這一輪先停：剩下的請求多半也會被拒，留到下一輪。
            var limited = outcomes.Where(x => x.Status == Platform.Google.GoogleStatus.RateLimited).ToArray();
            if (limited.Length > 0) return limited.Max(x => x.RetryAfter) ?? TimeSpan.Zero;
        }
        return null;
    }

    private static PushItem ItemOf(CalendarEvent row) => new(row.Id, row.CalendarId, row.GoogleEventId, row.SyncedHash is not null, row.RemovedAt is not null, row.LegacyUuid,
        new(row.DesNo, row.SegmentKey, row.Kind, row.ApplicantName, row.AgentName, row.FlowStatus, row.StartsAt, row.EndsAt));

    private static void Record(CalendarEvent row, PushOutcome outcome, DateTimeOffset now, CalendarSyncOptions settings, Tally run)
    {
        row.LastAttemptAt = now;
        row.UpdatedAt = now;
        if (outcome.Result == PushResult.Failed)
        {
            row.State = CalendarEventStates.Failed;
            row.Attempts++;
            var backoff = TimeSpan.FromMinutes(Math.Min(Math.Pow(2, Math.Min(row.Attempts - 1, 20)), settings.MaxRetryDelayMinutes));
            row.NextAttemptAt = now + (outcome.RetryAfter > backoff ? outcome.RetryAfter.Value : backoff);
            row.LastErrorCode = CalendarSyncFailures.Of(outcome.Status);
            row.LastError = Clip(outcome.Message ?? outcome.Status.ToString(), CalendarEvent.ErrorMaxLength);
            run.Failed++;
            return;
        }

        row.GoogleEventId = outcome.GoogleEventId;
        row.HtmlLink = outcome.HtmlLink is { } link ? Clip(link, CalendarEvent.HtmlLinkMaxLength) : row.HtmlLink;
        row.State = outcome.Result == PushResult.Deleted ? CalendarEventStates.Removed : CalendarEventStates.Synced;
        row.SyncedHash = outcome.Result == PushResult.Deleted ? row.SyncedHash : row.ContentHash;
        row.SyncedAt = now;
        row.Attempts = 0;
        row.NextAttemptAt = null;
        row.LastErrorCode = null;
        row.LastError = null;
        switch (outcome.Result)
        {
            case PushResult.Created: run.Created++; break;
            case PushResult.Updated: run.Updated++; break;
            default: run.Deleted++; break;
        }
    }

    private async Task<CalendarSyncOutcome> FinishAsync(CalendarSyncOptions settings, CalendarSyncRequest? request, Tally run, CancellationToken ct)
    {
        var finishedAt = clock.GetUtcNow();
        var status = run.ErrorCode is not null && run.ErrorCode != CalendarSyncFailures.RemovalsHeld ? SyncRunStatuses.Failed
            : run.Held > 0 ? SyncRunStatuses.Held
            : run.Failed > 0 ? SyncRunStatuses.Partial
            : SyncRunStatuses.Succeeded;
        // 上一筆紀錄的問題就是目前持續中的問題：問題出現、換了原因或恢復時都會寫一筆，所以最新一筆一定反映最近的轉換。
        var previous = await db.SyncRuns.AsNoTracking().Where(x => x.CalendarId == settings.CalendarId)
            .OrderByDescending(x => x.StartedAt).Select(x => x.ErrorCode).FirstOrDefaultAsync(CancellationToken.None);
        var changed = run.Created + run.Updated + run.Deleted > 0;
        // 同一個問題持續時不每一輪都寫紀錄：只在問題出現、換了原因或恢復時寫一次，其他輪看排程任務的執行紀錄。
        var newProblem = run.ErrorCode is not null && previous != run.ErrorCode;
        var restored = run.ErrorCode is null && previous is not null;
        SyncRun? record = null;
        if (request is not null || changed || run.Failed > 0 || newProblem || restored)
        {
            record = new SyncRun
            {
                CalendarId = settings.CalendarId,
                TriggerKind = request is null ? SyncRunTriggers.Scheduled : SyncRunTriggers.Manual,
                RequestedById = request?.RequestedBy is { } actor && actor != Guid.Empty ? actor : null,
                StartedAt = run.StartedAt,
                FinishedAt = finishedAt,
                Status = status,
                SourceCount = run.SourceCount,
                CreatedCount = run.Created,
                UpdatedCount = run.Updated,
                DeletedCount = run.Deleted,
                FailedCount = run.Failed,
                HeldCount = run.Held,
                ErrorCode = status is SyncRunStatuses.Failed or SyncRunStatuses.Held ? run.ErrorCode : null,
                ErrorMessage = run.ErrorMessage is { } message ? Clip(message, SyncRun.ErrorMessageMaxLength) : null,
            };
            db.SyncRuns.Add(record);
            if (changed)
                audit.Record(CalendarSyncAudit.Synced, Guid.Empty, resource: record.Id,
                    details: new { automatic = request is null, created = run.Created, updated = run.Updated, deleted = run.Deleted, failed = run.Failed });
            // 推送的結果已經寫回；這裡只剩紀錄，取消也要寫完，否則問題出現的那一輪會沒有紀錄。
            await db.SaveChangesAsync(CancellationToken.None);
        }

        // 日誌也只在狀態轉換時記，中斷期間不每一輪洗版。
        if (restored) LogRestored(logger);
        else if (newProblem && run.ErrorCode == CalendarSyncFailures.RemovalsHeld) LogRemovalsHeld(logger, run.Held);
        else if (newProblem) LogRunFailed(logger, run.ErrorCode!);
        if (changed) LogSynced(logger, run.Created, run.Updated, run.Deleted, run.Failed);
        return new(status, run.ErrorCode, run.Created, run.Updated, run.Deleted, run.Failed, run.Held, record);
    }

    private static string Clip(string text, int max) => text.Length <= max ? text : text[..max];

    /// <summary>一輪的累計。失敗與擋下分開記：擋下時其餘照常做完，失敗（GD、憑證）則是整輪的原因。</summary>
    private sealed class Tally(DateTimeOffset startedAt)
    {
        public DateTimeOffset StartedAt { get; } = startedAt;
        public int? SourceCount { get; set; }
        public int Created { get; set; }
        public int Updated { get; set; }
        public int Deleted { get; set; }
        public int Failed { get; set; }
        public int Held { get; set; }
        public string? ErrorCode { get; private set; }
        public string? ErrorMessage { get; private set; }

        public void Fail(string code, string? message)
        {
            ErrorCode = code;
            ErrorMessage = message;
        }

        public void Hold(int count)
        {
            Held = count;
            ErrorCode ??= CalendarSyncFailures.RemovalsHeld;
            ErrorMessage ??= $"{count} removals exceed the safety threshold and wait for confirmation.";
        }
    }

    [LoggerMessage(EventId = 3701, EventName = "calendar_sync.synced", Level = LogLevel.Information, Message = "Calendar sync changed Google Calendar: {Created} created, {Updated} updated, {Deleted} deleted, {Failed} failed.")]
    private static partial void LogSynced(ILogger logger, int created, int updated, int deleted, int failed);

    [LoggerMessage(EventId = 3702, EventName = "calendar_sync.run_failed", Level = LogLevel.Warning, Message = "Calendar sync cannot complete ({ErrorCode}); it will retry on the next interval.")]
    private static partial void LogRunFailed(ILogger logger, string errorCode);

    [LoggerMessage(EventId = 3703, EventName = "calendar_sync.restored", Level = LogLevel.Information, Message = "Calendar sync is working again.")]
    private static partial void LogRestored(ILogger logger);

    [LoggerMessage(EventId = 3704, EventName = "calendar_sync.removals_held", Level = LogLevel.Warning, Message = "Calendar sync held {Count} removals that exceed the safety threshold; an administrator must confirm them.")]
    private static partial void LogRemovalsHeld(ILogger logger, int count);

    [LoggerMessage(EventId = 3705, EventName = "calendar_sync.reset", Level = LogLevel.Information, Message = "Calendar sync reset the calendar: {Deleted} Google events deleted, {Events} event rows and {Runs} sync runs cleared.")]
    private static partial void LogReset(ILogger logger, int deleted, int events, int runs);
}
