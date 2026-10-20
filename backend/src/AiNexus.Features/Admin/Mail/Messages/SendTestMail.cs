using AiNexus.Data.App;
using AiNexus.Data.App.Mail;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Mail.Messages;

/// <summary>測試信的收件地址；使用者信箱上線後改為預設寄給操作者已驗證的信箱。</summary>
public sealed record MailTestRequest(string To);

internal sealed class MailTestRequestValidator : RequestValidator<MailTestRequest>
{
    public MailTestRequestValidator() => RuleFor(x => x.To).NotEmpty().MaximumLength(OutboundMessage.AddressMaxLength);
}

/// <summary>入列一封測試信（照常排隊、節流與重試），回傳訊息 ID 供在紀錄追蹤；寫行政稽核 <c>admin.mail_test</c>，不記收件地址。</summary>
internal sealed class SendTestMail(AppDbContext db, MailOutbox outbox, MailRenderer renderer, AdministrativeAudit audit, IOptions<MailOptions> options, TimeProvider clock)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPost("", (MailTestRequest body, IRequestActor actor, SendTestMail handler, CancellationToken ct) => handler.HandleAsync(actor.Id, body, ct).ToHttpResultAsync())
        .RequireRateLimiting(MailModule.TestRateLimit)
        .WithName("SendTestMail");

    public async Task<Result<MailEnqueuedDto>> HandleAsync(Guid actor, MailTestRequest request, CancellationToken ct)
    {
        if (!outbox.Available) return MailErrors.NotConfigured;
        MailEnqueuedDto? result = null;
        var outcome = await audit.MutateAsync(actor, FeatureCatalog.MailMessages, "admin.mail_test", null, "test",
            token => Snapshot(result?.MessageId, token), async () =>
            {
                var name = await db.Users.AsNoTracking().Where(x => x.Id == actor).Select(x => x.DisplayName).SingleAsync(ct);
                var settings = options.Value;
                var mail = renderer.Render(new TestMail(), new TestMailModel(name, clock.GetUtcNow(), settings.Transport, settings.From.Address));
                var enqueued = outbox.Enqueue(mail, new(request.To), new(RequestedBy: actor));
                if (!enqueued.IsSuccess) return enqueued.Error!;
                result = new(enqueued.Value!.MessageId, false);
                return Result.Success;
            }, ct);
        return outcome.IsSuccess ? result! : outcome.Error!;
    }

    private async Task<object?> Snapshot(Guid? created, CancellationToken ct)
        => created is { } id ? await db.Set<OutboundMessage>().AsNoTracking().Where(x => x.Id == id).Select(x => new { id = x.Id, status = x.Status, category = x.Category }).SingleOrDefaultAsync(ct) : null;
}
