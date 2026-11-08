using AiNexus.Data.App.CalendarSync;
using AiNexus.Features.Platform.Google;
using AiNexus.Features.Platform.Google.Calendar;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Office.CalendarSync.Sync;

/// <summary>要推送的一列，推送期間不變的快照（推送平行執行，不碰 DbContext）。</summary>
public sealed record PushItem(Guid Id, string CalendarId, string GoogleEventId, bool Created, bool Removed, string? LegacyUuid, AbsenceSegment Segment);

public enum PushResult
{
    Created,
    Updated,
    Deleted,
    Failed
}

/// <summary>一列的推送結果；失敗時 <see cref="Status"/> 是原因，<see cref="StopsRun"/> 表示換下一筆也會一樣失敗。</summary>
public sealed record PushOutcome(Guid Id, PushResult Result, string GoogleEventId, string? HtmlLink = null, GoogleStatus Status = GoogleStatus.Ok, string? Message = null, TimeSpan? RetryAfter = null)
{
    /// <summary>憑證、權限、設定或日曆本身不存在：整輪停下，不讓每一列都記一次失敗、也不替它們累計退避。</summary>
    public bool StopsRun => Result == PushResult.Failed && (Status is GoogleStatus.CredentialRejected or GoogleStatus.NotConfigured or GoogleStatus.Forbidden or GoogleStatus.NotFound);
}

/// <summary>
/// 把一列推到 Google，所有分支都是冪等的，同一列重推幾次結果都一樣：
/// <list type="bullet">
/// <item>建立用本地決定的事件 ID；409（ID 已存在，含上次建立成功卻沒寫回、或已刪除的事件）就改成整筆更新並設為 confirmed，等於接手或救回。</item>
/// <item>更新遇 404／410（有人在 Google 手動刪了、或日曆被清過）就重新建立。</item>
/// <item>刪除遇 404／410 視為已完成。</item>
/// <item>舊系統轉入、還沒確認建立過的列，建立或移除前都先以舊系統寫在事件上的 UUID 找回事件，避免重複或殘留。</item>
/// </list>
/// 這裡不重試：失敗交給呼叫端記錄並排定下次重試時間。
/// </summary>
[Singleton]
public sealed class CalendarEventPusher(IGoogleCalendar calendar)
{
    public async Task<PushOutcome> PushAsync(PushItem item, CancellationToken ct)
    {
        var eventId = item.GoogleEventId;
        // 還沒確認建立過的舊系統列：事件可能在 Google 上、ID 是舊系統的。移除與建立都要先找回它，否則移除會留下舊事件、建立會多一個。
        if (!item.Created && item.LegacyUuid is { } uuid)
        {
            var found = await calendar.FindEventsAsync(item.CalendarId, CalendarEventContent.LegacyUuidProperty, uuid, ct);
            if (!found.IsOk) return Failure(item, eventId, found);
            if (found.Value!.FirstOrDefault(x => x.Id is not null) is { Id: { } legacyId })
            {
                if (item.Removed) return await DeleteAsync(item, legacyId, ct);
                var adopted = await calendar.UpdateEventAsync(item.CalendarId, legacyId, CalendarEventContent.Render(item.Id, legacyId, item.Segment), ct);
                if (adopted.IsOk) return new(item.Id, PushResult.Created, legacyId, adopted.Value!.HtmlLink);
                // 找到之後才被刪掉：當成沒有舊事件，照一般流程建立。404 在這裡是事件不見，不是日曆找不到，不能停下整輪。
                if (adopted.Status is not (GoogleStatus.NotFound or GoogleStatus.Gone)) return Failure(item, legacyId, adopted);
            }
        }
        if (item.Removed) return await DeleteAsync(item, eventId, ct);

        if (item.Created)
        {
            var updated = await calendar.UpdateEventAsync(item.CalendarId, eventId, CalendarEventContent.Render(item.Id, eventId, item.Segment), ct);
            if (updated.IsOk) return new(item.Id, PushResult.Updated, eventId, updated.Value!.HtmlLink);
            if (updated.Status is not (GoogleStatus.NotFound or GoogleStatus.Gone)) return Failure(item, eventId, updated);
        }
        return await CreateAsync(item, eventId, ct);
    }

    private async Task<PushOutcome> DeleteAsync(PushItem item, string eventId, CancellationToken ct)
    {
        var deleted = await calendar.DeleteEventAsync(item.CalendarId, eventId, ct);
        return deleted.Status is GoogleStatus.Ok or GoogleStatus.NotFound or GoogleStatus.Gone
            ? new(item.Id, PushResult.Deleted, eventId)
            : Failure(item, eventId, deleted);
    }

    private async Task<PushOutcome> CreateAsync(PushItem item, string eventId, CancellationToken ct)
    {
        var body = CalendarEventContent.Render(item.Id, eventId, item.Segment);
        var inserted = await calendar.InsertEventAsync(item.CalendarId, body, ct);
        if (inserted.IsOk) return new(item.Id, PushResult.Created, eventId, inserted.Value!.HtmlLink);
        if (inserted.Status == GoogleStatus.Conflict)
        {
            var adopted = await calendar.UpdateEventAsync(item.CalendarId, eventId, body, ct);
            return adopted.IsOk ? new(item.Id, PushResult.Created, eventId, adopted.Value!.HtmlLink) : Failure(item, eventId, adopted);
        }
        // 舊系統的事件 ID 是 Google 產生的，格式不一定能自己指定；被拒時改用本地主鍵當 ID 重新建立。
        var own = CalendarEvent.NewGoogleEventId(item.Id);
        if (inserted.Status == GoogleStatus.Invalid && eventId != own) return await CreateAsync(item, own, ct);
        return Failure(item, eventId, inserted);
    }

    private static PushOutcome Failure<T>(PushItem item, string eventId, GoogleResponse<T> response)
        => new(item.Id, PushResult.Failed, eventId, Status: response.Status, Message: response.Message, RetryAfter: response.RetryAfter);
}
