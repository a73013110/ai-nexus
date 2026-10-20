using System.Diagnostics;
using AiNexus.Data.App;
using AiNexus.Data.App.Mail;
using AiNexus.Features.Admin.Mail.Delivery;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Mail;

/// <summary>收件者。<paramref name="UserId"/> 是平台使用者時填入，供日後依使用者查寄送紀錄。</summary>
public sealed record MailRecipient(string Address, string? DisplayName = null, Guid? UserId = null);

/// <summary>
/// 入列選項。冪等鍵慣例 <c>&lt;category&gt;:&lt;業務 ID&gt;[:&lt;收件者 UserId&gt;]</c>；<paramref name="ExpiresAt"/> 之後不再寄出（驗證碼）。
/// </summary>
public sealed record MailEnqueueOptions(string? IdempotencyKey = null, DateTimeOffset? ExpiresAt = null, Guid? RequestedBy = null, Guid? BatchId = null);

/// <summary><paramref name="Duplicate"/> 為 true 表示冪等鍵已存在，回傳的是既有那封。</summary>
public sealed record MailEnqueued(Guid MessageId, bool Duplicate);

/// <summary>批次入列的結果；<paramref name="Rejected"/> 是地址不合法或網域不允許、直接寫成 failed 的數量。</summary>
public sealed record MailBatch(Guid BatchId, IReadOnlyList<Guid> MessageIds, int Rejected);

/// <summary>呼叫端顯示「寄送中／已寄出／失敗」用，不含收件者與內文。<paramref name="Status"/> 見 <see cref="MailStates"/>。</summary>
public sealed record MailDelivery(Guid MessageId, string Status, DateTimeOffset? SentAt, string? ErrorCode);

/// <summary>
/// 其他模組寄信的唯一入口（transactional outbox）。只把信件加入呼叫端的 <see cref="AppDbContext"/>，不呼叫 <c>SaveChanges</c>：
/// 由呼叫端與業務變更一起儲存，業務失敗就不會寄出，寄信也不會在業務提交前發生。實際寄送由 <see cref="MailDispatcher"/> 在背景進行。
/// </summary>
public sealed class MailOutbox(AppDbContext db, MailRenderer renderer, MailContents contents, IOptions<MailOptions> options, TimeProvider clock)
{
    /// <summary>傳輸已設定（Smtp 或 Pickup）。Disabled 時仍可入列，只是不會寄出；需要使用者等信的流程應先檢查並回 <see cref="MailErrors.NotConfigured"/>。</summary>
    public bool Available => options.Value.Enabled;

    /// <summary>單封。地址不合法或網域不允許時回傳錯誤、不加入任何列；冪等鍵已存在時回傳既有那封。</summary>
    public async Task<Result<MailEnqueued>> EnqueueAsync<TModel>(MailTemplate<TModel> template, TModel model, MailRecipient to, MailEnqueueOptions? enqueue, CancellationToken ct)
    {
        enqueue ??= new();
        if (await ExistingAsync(enqueue.IdempotencyKey, ct) is { } existing) return new MailEnqueued(existing, true);
        if (Check(to.Address) is { } error) return error;
        return new MailEnqueued(Add(renderer.Render(template, model), to, enqueue, null).Id, false);
    }

    /// <summary>
    /// 批次。每位收件者一列、同一 BatchId；不合法的收件者直接寫成 failed 列（紀錄看得到），不讓整批失敗。
    /// 冪等鍵在批次內以 <c>:&lt;序號&gt;</c> 區分，同一批重送時已入列的收件者會略過。約 2000 封以上請由呼叫端以 durable job 分段入列。
    /// </summary>
    public async Task<MailBatch> EnqueueManyAsync<TModel>(MailTemplate<TModel> template, IEnumerable<(MailRecipient To, TModel Model)> items, MailEnqueueOptions enqueue, CancellationToken ct)
    {
        var batch = enqueue.BatchId ?? Guid.NewGuid();
        var ids = new List<Guid>();
        var rejected = 0;
        var index = 0;
        foreach (var (to, model) in items)
        {
            var options = enqueue with { BatchId = batch, IdempotencyKey = enqueue.IdempotencyKey is { } key ? $"{key}:{index}" : null };
            index++;
            if (await ExistingAsync(options.IdempotencyKey, ct) is { } existing) { ids.Add(existing); continue; }
            var rendered = renderer.Render(template, model);
            if (Check(to.Address) is { } error)
            {
                rejected++;
                ids.Add(AddRejected(rendered, to, options, error.Code).Id);
                continue;
            }
            ids.Add(Add(rendered, to, options, null).Id);
        }
        return new(batch, ids, rejected);
    }

    /// <summary>目前寄送狀態；不存在（已依保存期限刪除）時為 null。</summary>
    public Task<MailDelivery?> GetDeliveryAsync(Guid messageId, CancellationToken ct)
        => db.Set<OutboundMessage>().AsNoTracking().Where(x => x.Id == messageId)
            .Select(x => new MailDelivery(x.Id, x.Status, x.SentAt, x.ErrorCode)).SingleOrDefaultAsync(ct);

    /// <summary>
    /// 取消尚未寄出的信並刪除內文（例如驗證碼被新的取代）；立即在資料庫執行，會加入呼叫端進行中的交易。已在寄送中或已結束時回傳 false。
    /// </summary>
    public async Task<bool> CancelAsync(Guid messageId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var cancelled = await db.Set<OutboundMessage>().Where(x => x.Id == messageId && x.Status == MailStates.Queued)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.Status, MailStates.Cancelled).SetProperty(x => x.UpdatedAt, now).SetProperty(x => x.ContentPurgedAt, now), ct);
        if (cancelled == 1) await db.Set<OutboundMessageContent>().Where(x => x.MessageId == messageId).ExecuteDeleteAsync(ct);
        return cancelled == 1;
    }

    /// <summary>重寄：沿用原信渲染好的內容建立新信（<c>ResendOfId</c>），原紀錄不變。</summary>
    internal OutboundMessage AddCopy(OutboundMessage original, OutboundMessageContent content, string address, MailEnqueueOptions enqueue)
    {
        var rendered = new RenderedMail(original.Category, original.Priority, false, original.Subject, content.Html ?? "", content.Text);
        var message = Add(rendered, new(address, original.RecipientName, original.RecipientUserId), enqueue, original.Id);
        db.Set<OutboundMessageContent>().Local.Single(x => x.MessageId == message.Id).Html = content.Html;
        return message;
    }

    /// <summary>入列已渲染好的信（測試信），不經範本。</summary>
    internal Result<MailEnqueued> Enqueue(RenderedMail mail, MailRecipient to, MailEnqueueOptions enqueue)
        => Check(to.Address) is { } error ? error : new MailEnqueued(Add(mail, to, enqueue, null).Id, false);

    internal async Task<Guid?> ExistingAsync(string? key, CancellationToken ct)
    {
        if (key is null) return null;
        if (key.Length > OutboundMessage.IdempotencyKeyMaxLength) throw new ArgumentException("Idempotency key is too long.", nameof(key));
        // 同一交易裡尚未儲存的列只存在 Local，資料庫查不到，所以兩邊都要檢查；唯一索引再擋一次並行。
        return db.Set<OutboundMessage>().Local.FirstOrDefault(x => x.IdempotencyKey == key)?.Id
            ?? await db.Set<OutboundMessage>().Where(x => x.IdempotencyKey == key).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
    }

    private Error? Check(string address)
    {
        if (!MailAddresses.TryNormalize(address, out var normalized)) return MailErrors.AddressInvalid;
        return MailAddresses.Allowed(normalized, options.Value.AllowedRecipientDomains) ? null : MailErrors.RecipientBlocked;
    }

    private OutboundMessage Add(RenderedMail mail, MailRecipient to, MailEnqueueOptions enqueue, Guid? resendOf)
    {
        MailAddresses.TryNormalize(to.Address, out var address);
        var message = Message(mail, to, enqueue, resendOf);
        message.RecipientAddress = address;
        message.RecipientMasked = MailAddresses.Mask(address);
        message.RecipientHash = MailAddresses.Hash(address);
        db.Add(message);
        db.Add(contents.Create(message.Id, mail));
        return message;
    }

    private OutboundMessage AddRejected(RenderedMail mail, MailRecipient to, MailEnqueueOptions enqueue, string code)
    {
        var raw = to.Address.Trim();
        var message = Message(mail, to, enqueue, null);
        message.Status = MailStates.Failed;
        message.ErrorCode = code;
        message.RecipientAddress = null;
        message.RecipientName = null;
        message.RecipientMasked = MailAddresses.Mask(raw.Length > OutboundMessage.AddressMaxLength ? raw[..OutboundMessage.AddressMaxLength] : raw);
        message.RecipientHash = MailAddresses.Hash(raw);
        message.ContentPurgedAt = message.CreatedAt;
        db.Add(message);
        return message;
    }

    private OutboundMessage Message(RenderedMail mail, MailRecipient to, MailEnqueueOptions enqueue, Guid? resendOf)
    {
        var now = clock.GetUtcNow();
        var settings = options.Value;
        var id = Guid.NewGuid();
        var name = to.DisplayName?.Trim();
        return new()
        {
            Id = id,
            Category = mail.Category,
            Priority = mail.Priority,
            Sensitive = mail.Sensitive,
            IdempotencyKey = enqueue.IdempotencyKey,
            BatchId = enqueue.BatchId,
            ResendOfId = resendOf,
            RequestedById = enqueue.RequestedBy,
            RecipientUserId = to.UserId,
            RecipientName = string.IsNullOrEmpty(name) ? null : name.Length > OutboundMessage.NameMaxLength ? name[..OutboundMessage.NameMaxLength] : name,
            FromAddress = settings.From.Address,
            Subject = mail.Subject,
            // 固定的 Message-ID 讓「到底有沒有寄出」能拿去 mail server 或 Google 的紀錄比對；租約過期重寄同一列時也不變，收件端有機會去重。
            HeaderMessageId = $"<{id:N}@{(settings.From.Address.Contains('@') ? settings.MessageIdHost : "localhost")}>",
            NextAttemptAt = now,
            ExpiresAt = enqueue.ExpiresAt,
            TraceId = Activity.Current?.TraceId.ToHexString(),
            CreatedAt = now,
            UpdatedAt = now,
        };
    }
}
