using System.Security.Cryptography;
using AiNexus.Data.App;
using AiNexus.Data.App.Mail;
using AiNexus.Platform.Data.Sql;
using AiNexus.Platform.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Mail.Delivery;

/// <summary>
/// 寄送 outbox 的背景工作（單一 worker，ADR 0001）。每一輪依節流額度與 <c>MaxPerDay</c> 以 <c>UPDLOCK, READPAST</c> 批次認領、
/// 分到 <c>MaxConcurrency</c> 條 lane，每條 lane 用同一條連線連續寄送，結果以 <c>LeaseToken</c> fencing 寫回並記一筆嘗試。
/// 保證 at-least-once：SMTP 已接受但寫回前程序中止，租約到期後會再寄一次（Message-ID 不變，收件端有機會去重）。
/// </summary>
public sealed partial class MailDispatcher(
    IServiceScopeFactory scopes,
    StorageReadiness readiness,
    IMailTransport transport,
    MailCircuit circuit,
    MailThrottle throttle,
    IOptions<MailOptions> options,
    Issues issues,
    ILogger<MailDispatcher> logger,
    TimeProvider clock) : BackgroundService
{
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(120);

    /// <summary>每條 lane 一輪最多寄幾封；一輪結束才關連線，太大會讓斷路器與停機反應變慢。</summary>
    public const int PerLane = 10;

    // 連線或傳輸層故障對每一封都一樣，與信本身無關；故障排除後不該讓它們繼續等原本的退避時間。4xx（mail_deferred）可能是灰名單，照原退避。
    private static readonly string[] Deferrable = [MailFailures.TransportUnavailable, MailFailures.ConnectionFailed, MailFailures.Timeout];

    private bool resumedAtStart;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var claimed = readiness.Configured ? await DispatchAsync(stoppingToken) : 0;
                if (claimed == 0) await Task.Delay(PollInterval, clock, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogQueueUnavailable(logger, ex.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(10), clock, stoppingToken);
            }
        }
    }

    /// <summary>執行一輪，回傳認領的封數（0 表示沒有可寄的信、額度用完、斷路器開著或傳輸停用）。測試直接呼叫。</summary>
    public async Task<int> DispatchAsync(CancellationToken stop)
    {
        var settings = options.Value;
        if (!settings.Enabled || circuit.IsOpen) return 0;
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!resumedAtStart)
        {
            // 設定多半是改完重啟才生效（IOptions），重啟前因故障延後的信沒有理由再等。
            await AdvanceAsync(db, "startup", stop);
            resumedAtStart = true;
        }
        var now = clock.GetUtcNow();
        var wanted = settings.MaxConcurrency * PerLane;
        if (settings.MaxPerDay > 0)
        {
            var day = now.AddDays(-1);
            wanted = Math.Min(wanted, settings.MaxPerDay - await db.Set<OutboundMessage>().CountAsync(x => x.SentAt >= day, stop));
            if (wanted <= 0) return 0;
        }
        var allowed = throttle.Take(wanted);
        if (allowed == 0) return 0;

        var lease = Guid.NewGuid();
        IReadOnlyList<Claim> claimed;
        try
        {
            // 認領條件、隔離層級與鎖定見 procedure 檔頭；只改狀態、不寫稽核，所以不用 ExecuteProcedureAsync。PreviousStatus 讓下面補記 abandoned。
            claimed = await scope.ServiceProvider.GetRequiredService<ISqlDatabase<AppDbContext>>().QueryProcedureAsync<Claim>("mail.usp_ClaimOutboundMessages",
                new { Take = allowed, Now = now, Lease = lease, LeaseUntil = now + LeaseDuration }, cancellationToken: stop);
        }
        catch
        {
            throttle.Return(allowed);
            throw;
        }
        throttle.Return(allowed - claimed.Count);
        if (claimed.Count == 0) return 0;
        await RecordAbandonedAsync(db, claimed, now);
        var autoRetry = await MailDeliverySettings.AutoRetryAsync(db, CancellationToken.None);

        var started = clock.GetTimestamp();
        // 依優先序輪流分配，每條 lane 都先寄安全信。
        var ordered = claimed.OrderByDescending(x => x.Priority).ThenBy(x => x.NextAttemptAt).Select(x => x.Id).ToList();
        var lanes = Math.Min(settings.MaxConcurrency, ordered.Count);
        var sent = (await Task.WhenAll(Enumerable.Range(0, lanes).Select(lane => RunLaneAsync([.. ordered.Where((_, i) => i % lanes == lane)], lease, autoRetry, stop)))).Sum();
        var elapsed = (long)clock.GetElapsedTime(started).TotalMilliseconds;
        if (sent > 0) LogBatchSent(logger, sent, elapsed);
        return claimed.Count;
    }

    private async Task<int> RunLaneAsync(List<Guid> ids, Guid lease, bool autoRetry, CancellationToken stop)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var contents = scope.ServiceProvider.GetRequiredService<MailContents>();
        var sent = 0;
        await using var session = await transport.OpenAsync(stop);
        for (var i = 0; i < ids.Count; i++)
        {
            // 斷路器開了（這條或另一條 lane 遇到傳輸層故障）或正在停機：剩下的信退回佇列、不記嘗試，也不算一次。
            if (circuit.IsOpen || stop.IsCancellationRequested)
            {
                await ReleaseAsync(db, ids[i..], lease);
                break;
            }
            if (await DeliverAsync(db, contents, session, ids[i], lease, autoRetry, stop)) sent++;
        }
        return sent;
    }

    private async Task<bool> DeliverAsync(AppDbContext db, MailContents contents, IMailSession session, Guid id, Guid lease, bool autoRetry, CancellationToken stop)
    {
        db.ChangeTracker.Clear();
        var message = await db.Set<OutboundMessage>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.LeaseToken == lease, stop);
        if (message is null) return false;
        var settings = options.Value;
        var started = clock.GetUtcNow();
        if (Refuse(message, settings, started) is { } refused)
        {
            await FinishAsync(db, message, lease, started, refused, null);
            return false;
        }
        var content = await db.Set<OutboundMessageContent>().AsNoTracking().SingleOrDefaultAsync(x => x.MessageId == id, stop);
        if ((content is null ? null : contents.Read(content)) is not { } body)
        {
            await FinishAsync(db, message, lease, started, Local(MailErrors.ContentUnavailable.Code), null);
            return false;
        }
        var (html, text) = body;

        string response;
        try
        {
            var mime = MimeComposer.Compose(message, message.RecipientAddress!, html, text, settings, started);
            response = await session.SendAsync(new(message.Id, mime), stop);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            // 停機：保留租約，到期後由下一次認領補記 abandoned 並重寄。
            throw;
        }
        catch (Exception ex)
        {
            var failure = MailFailures.Classify(ex);
            var issue = failure.Unexpected ? issues.Report(ex, MailFailures.DeliveryFailed, LogLevel.Warning) : null;
            var stage = failure.Stage ?? "unknown";
            var reason = failure.Reason ?? failure.ErrorType;
            if (failure.Outcome == MailAttemptOutcomes.Transport)
            {
                if (circuit.Open(failure.ErrorCode)) LogTransportUnavailable(logger, failure.ErrorCode, stage, reason);
            }
            else if (failure.Connection && circuit.ConnectionFailed(failure.ErrorCode))
            {
                LogTransportUnavailable(logger, failure.ErrorCode, stage, reason);
            }
            // 自動重試關閉時，暫時性與傳輸層失敗也直接結束；斷路器照常開啟，佇列裡其他信不會一起被耗掉。
            var retry = autoRetry && failure.Outcome != MailAttemptOutcomes.Permanent;
            await FinishAsync(db, message, lease, started, failure, issue, retry: retry);
            if (retry) LogDeliveryRetry(logger, message.Id, message.Attempts, failure.ErrorCode, stage, reason);
            else LogDeliveryFailed(logger, message.Id, failure.ErrorCode, stage, reason);
            return false;
        }

        var restored = circuit.Succeeded();
        await FinishAsync(db, message, lease, started, null, null, response);
        if (restored)
        {
            LogTransportRestored(logger);
            await AdvanceAsync(db, "restored", CancellationToken.None);
        }
        return true;
    }

    /// <summary>不必連線就知道不能寄的情況：過期、超過放棄時間、原址已清除、網域已不在允許清單。</summary>
    private static MailFailure? Refuse(OutboundMessage message, MailOptions settings, DateTimeOffset now)
    {
        if (message.ExpiresAt is { } expires && expires <= now) return Local(MailFailures.Expired);
        if (message.CreatedAt.AddHours(settings.GiveUpAfterHours) <= now) return Local(MailFailures.GaveUp);
        if (message.RecipientAddress is null) return Local(MailErrors.RecipientUnavailable.Code);
        return MailAddresses.Allowed(message.RecipientAddress, settings.AllowedRecipientDomains) ? null : Local(MailErrors.RecipientBlocked.Code);
    }

    private static MailFailure Local(string code) => new(MailAttemptOutcomes.Permanent, code, null, null, false, false, "local");

    /// <summary>
    /// 以租約 fencing 寫回結果並記一筆嘗試（同一交易）。寄出後一律用 <see cref="CancellationToken.None"/>，停機也要寫完，否則會重寄。
    /// 敏感信一結束（寄出或失敗）就刪除內文。<paramref name="retry"/> 為 false 時失敗即結束（永久性失敗或自動重試關閉）。
    /// </summary>
    private async Task FinishAsync(AppDbContext db, OutboundMessage message, Guid lease, DateTimeOffset started, MailFailure? failure, string? issue, string? response = null, bool retry = false)
    {
        var now = clock.GetUtcNow();
        var outcome = failure?.Outcome ?? MailAttemptOutcomes.Sent;
        var status = outcome == MailAttemptOutcomes.Sent ? MailStates.Sent : retry ? MailStates.Queued : MailStates.Failed;
        var next = outcome == MailAttemptOutcomes.Transport ? circuit.OpenUntil ?? now
            : now + MailFailures.Backoff(message.Attempts, RandomNumberGenerator.GetInt32(1000) / 1000d);
        var redacted = response is null ? null : DiagnosticRedactor.Text(response, OutboundMessage.ResponseMaxLength);
        var purge = message.Sensitive && status != MailStates.Queued;
        var transportName = transport.Name;

        await using var transaction = await db.Database.BeginTransactionAsync(CancellationToken.None);
        var updated = await db.Set<OutboundMessage>().Where(x => x.Id == message.Id && x.LeaseToken == lease && x.Status == MailStates.Sending)
            .ExecuteUpdateAsync(p => p
                .SetProperty(x => x.Status, status)
                .SetProperty(x => x.LeaseToken, (Guid?)null)
                .SetProperty(x => x.LeaseUntil, (DateTimeOffset?)null)
                .SetProperty(x => x.Transport, transportName)
                .SetProperty(x => x.SentAt, x => status == MailStates.Sent ? now : x.SentAt)
                .SetProperty(x => x.ProviderResponse, x => status == MailStates.Sent ? redacted : x.ProviderResponse)
                .SetProperty(x => x.NextAttemptAt, x => status == MailStates.Queued ? next : x.NextAttemptAt)
                .SetProperty(x => x.ErrorCode, failure == null ? null : failure.ErrorCode)
                .SetProperty(x => x.IssueCode, issue)
                .SetProperty(x => x.ContentPurgedAt, x => purge ? now : x.ContentPurgedAt)
                .SetProperty(x => x.UpdatedAt, now), CancellationToken.None);
        // 租約已被接手（寄送超過 LeaseDuration）：結果交給新的持有者，不覆寫。
        if (updated == 0) return;
        if (purge) await db.Set<OutboundMessageContent>().Where(x => x.MessageId == message.Id).ExecuteDeleteAsync(CancellationToken.None);
        db.Add(new DeliveryAttempt
        {
            MessageId = message.Id,
            Attempt = message.Attempts,
            Transport = transportName,
            StartedAt = started,
            FinishedAt = now,
            Outcome = outcome,
            SmtpStatus = failure?.SmtpStatus,
            EnhancedStatus = failure?.EnhancedStatus,
            Response = redacted ?? failure?.Detail,
            ErrorCode = failure?.ErrorCode,
            IssueCode = issue,
            Stage = failure?.Stage,
        });
        await db.SaveChangesAsync(CancellationToken.None);
        await transaction.CommitAsync(CancellationToken.None);
    }

    /// <summary>沒寄就退回：撤回認領時加的次數，歸還節流額度。</summary>
    private async Task ReleaseAsync(AppDbContext db, List<Guid> ids, Guid lease)
    {
        var now = clock.GetUtcNow();
        var released = await db.Set<OutboundMessage>().Where(x => ids.Contains(x.Id) && x.LeaseToken == lease && x.Status == MailStates.Sending)
            .ExecuteUpdateAsync(p => p
                .SetProperty(x => x.Status, MailStates.Queued)
                .SetProperty(x => x.Attempts, x => x.Attempts - 1)
                .SetProperty(x => x.LeaseToken, (Guid?)null)
                .SetProperty(x => x.LeaseUntil, (DateTimeOffset?)null)
                .SetProperty(x => x.UpdatedAt, now), CancellationToken.None);
        throttle.Return(released);
    }

    /// <summary>接手過期租約的列：上一次嘗試沒有結果，補一筆 abandoned，管理頁才看得出「寄到一半中止」。</summary>
    private async Task RecordAbandonedAsync(AppDbContext db, IReadOnlyList<Claim> claimed, DateTimeOffset now)
    {
        var abandoned = claimed.Where(x => x.PreviousStatus == MailStates.Sending && x.Attempts > 1).ToList();
        if (abandoned.Count == 0) return;
        var ids = abandoned.Select(x => x.Id).ToList();
        var recorded = await db.Set<DeliveryAttempt>().Where(x => ids.Contains(x.MessageId)).Select(x => new { x.MessageId, x.Attempt }).ToListAsync(CancellationToken.None);
        foreach (var claim in abandoned.Where(x => !recorded.Any(r => r.MessageId == x.Id && r.Attempt == x.Attempts - 1)))
            db.Add(new DeliveryAttempt { MessageId = claim.Id, Attempt = claim.Attempts - 1, Transport = transport.Name, StartedAt = now, FinishedAt = now, Outcome = MailAttemptOutcomes.Abandoned });
        await db.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>傳輸恢復或程序啟動時，把因連線或傳輸層故障延後的信提前到現在（單一 UPDATE，走 Pending 篩選索引）。</summary>
    private async Task AdvanceAsync(AppDbContext db, string reason, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var advanced = await db.Set<OutboundMessage>()
            .Where(x => x.Status == MailStates.Queued && x.NextAttemptAt > now && x.ErrorCode != null && Deferrable.Contains(x.ErrorCode))
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.NextAttemptAt, now).SetProperty(x => x.UpdatedAt, now), ct);
        if (advanced > 0) LogQueueAdvanced(logger, advanced, reason);
    }

    private sealed record Claim(Guid Id, int Attempts, MailPriority Priority, DateTimeOffset NextAttemptAt, string PreviousStatus);

    [LoggerMessage(EventId = 3300, EventName = "mail.batch_sent", Level = LogLevel.Information, Message = "Mail dispatcher sent {Count} messages in {ElapsedMs} ms.")]
    private static partial void LogBatchSent(ILogger logger, int count, long elapsedMs);

    [LoggerMessage(EventId = 3301, EventName = "mail.delivery_retry", Level = LogLevel.Warning, Message = "Mail {MailMessageId} will retry after attempt {Attempt} ({ErrorCode} at {Stage}: {Reason}).")]
    private static partial void LogDeliveryRetry(ILogger logger, Guid mailMessageId, int attempt, string errorCode, string stage, string reason);

    [LoggerMessage(EventId = 3302, EventName = "mail.delivery_failed", Level = LogLevel.Warning, Message = "Mail {MailMessageId} failed ({ErrorCode} at {Stage}: {Reason}).")]
    private static partial void LogDeliveryFailed(ILogger logger, Guid mailMessageId, string errorCode, string stage, string reason);

    [LoggerMessage(EventId = 3303, EventName = "mail.transport_unavailable", Level = LogLevel.Error, Message = "Mail transport unavailable ({ErrorCode} at {Stage}: {Reason}); sending paused. Run the connection diagnosis on the mail page for details.")]
    private static partial void LogTransportUnavailable(ILogger logger, string errorCode, string stage, string reason);

    [LoggerMessage(EventId = 3304, EventName = "mail.transport_restored", Level = LogLevel.Information, Message = "Mail transport restored; sending resumed.")]
    private static partial void LogTransportRestored(ILogger logger);

    [LoggerMessage(EventId = 3307, EventName = "mail.queue_advanced", Level = LogLevel.Information, Message = "Moved {Count} mails deferred by transport failures to now ({Reason}).")]
    private static partial void LogQueueAdvanced(ILogger logger, int count, string reason);

    [LoggerMessage(EventId = 3305, EventName = "mail.queue_unavailable", Level = LogLevel.Warning, Message = "Mail queue unavailable ({ErrorType}).")]
    private static partial void LogQueueUnavailable(ILogger logger, string errorType);
}
