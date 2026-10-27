using AiNexus.Data.App;
using AiNexus.Data.App.Mail;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Mail.Messages;

/// <summary>
/// 寄信測試。<paramref name="Category"/> 是已登記的系統信分類（<c>GET /admin/mail-samples</c>），空白＝設定測試信 <c>mail.test</c>；
/// <paramref name="Parameters"/> 以模型參數名對應字串值，沒填的用範例值。
/// </summary>
public sealed record MailTestRequest(string To, string? Category = null, IReadOnlyDictionary<string, string?>? Parameters = null);

internal sealed class MailTestRequestValidator : RequestValidator<MailTestRequest>
{
    public const int MaxParameters = 20, ValueMaxLength = 2000;

    public MailTestRequestValidator()
    {
        RuleFor(x => x.To).NotEmpty().MaximumLength(OutboundMessage.AddressMaxLength);
        RuleFor(x => x.Category).MaximumLength(OutboundMessage.CategoryMaxLength);
        RuleFor(x => x.Parameters).Must(x => x is null || x.Count <= MaxParameters && x.All(p => p.Key.Length <= 64 && (p.Value?.Length ?? 0) <= ValueMaxLength))
            .WithMessage($"最多 {MaxParameters} 個參數，每個值不超過 {ValueMaxLength} 字元。");
    }
}

/// <summary>
/// 以任一已登記的系統信範本入列一封測試信（照常排隊、節流與重試），回傳訊息 ID 供在紀錄追蹤。分類維持範本原值，紀錄可依分類篩選；
/// 主旨加「[測試]」以免收件者或查紀錄的人當成真的通知。寫行政稽核 <c>admin.mail_test</c>，不記收件地址與參數值（驗證碼信的參數可能像真的驗證碼）。
/// </summary>
internal sealed class SendTestMail(AppDbContext db, MailOutbox outbox, MailRenderer renderer, IEnumerable<MailSample> samples, AdministrativeAudit audit,
    IOptions<MailOptions> options, TimeProvider clock)
{
    public const string SubjectPrefix = "[測試] ";

    public static void Map(RouteGroupBuilder routes) => routes
        .MapPost("", (MailTestRequest body, IRequestActor actor, SendTestMail handler, CancellationToken ct) => handler.HandleAsync(actor.Id, body, ct).ToHttpResultAsync())
        .RequireRateLimiting(MailModule.TestRateLimit)
        .Audited(MailAudit.TestMessageSent)
        .WithName("SendTestMail");

    public async Task<Result<MailEnqueuedDto>> HandleAsync(Guid actor, MailTestRequest request, CancellationToken ct)
    {
        if (!outbox.Available) return MailErrors.NotConfigured;
        var category = string.IsNullOrWhiteSpace(request.Category) ? TestMail.TestCategory : request.Category.Trim();
        if (samples.FirstOrDefault(x => x.Category == category) is not { } sample) return MailErrors.SampleNotFound;
        // 渲染只用參數與設定、不碰資料庫，參數錯誤在進入行政寫入鎖之前就回錯誤。
        var rendered = sample.Render(renderer, ListMailSamples.Context(options.Value, clock), request.Parameters ?? new Dictionary<string, string?>());
        if (!rendered.IsSuccess) return rendered.Error;
        var mail = rendered.Value;
        if (category != TestMail.TestCategory) mail = mail with { Subject = MailRenderer.Subject(SubjectPrefix + mail.Subject) };
        MailEnqueuedDto? result = null;
        var outcome = await audit.MutateAsync(actor, FeatureCatalog.MailMessages, null, "test",
            token => Snapshot(result?.MessageId, token), () =>
            {
                var enqueued = outbox.Enqueue(mail, new(request.To), new(RequestedBy: actor));
                if (!enqueued.IsSuccess) return Task.FromResult<Result>(enqueued.Error);
                result = new(enqueued.Value.MessageId, false);
                return Task.FromResult(Result.Success);
            }, ct);
        return outcome.IsSuccess ? result! : outcome.Error!;
    }

    private async Task<object?> Snapshot(Guid? created, CancellationToken ct)
        => created is { } id ? await db.Set<OutboundMessage>().AsNoTracking().Where(x => x.Id == id).Select(x => new { id = x.Id, status = x.Status, category = x.Category }).SingleOrDefaultAsync(ct) : null;
}
