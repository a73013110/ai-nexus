using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Mail.Delivery;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Mail.Transport;

/// <summary>連線診斷的收件地址（只送 RCPT TO 檢查，不寄出）；空值用寄件地址。</summary>
public sealed record MailDiagnosisRequest(string? To);

internal sealed class MailDiagnosisRequestValidator : RequestValidator<MailDiagnosisRequest>
{
    public MailDiagnosisRequestValidator() => RuleFor(x => x.To).MaximumLength(254);
}

/// <summary>
/// 診斷的一步。<paramref name="Id"/> 見 <see cref="MailDiagnosisSteps"/>，<paramref name="Status"/> 見 <see cref="MailDiagnosisStatuses"/>；
/// <paramref name="Summary"/> 含伺服器回應原文（只回給發起的人，不寫入紀錄）。
/// </summary>
public sealed record MailDiagnosisStep(string Id, string Status, string Summary, long ElapsedMs, int? SmtpStatus);

/// <summary>
/// 連線診斷的結果。<paramref name="Transcript"/> 是 SMTP 對話紀錄（AUTH 已遮罩）；<paramref name="Certificate"/> 有值代表收到伺服器憑證（包含被拒的）。
/// </summary>
public sealed record MailDiagnosisDto(bool Succeeded, string Transport, string? Server, DateTimeOffset StartedAt, long ElapsedMs,
    IReadOnlyList<MailDiagnosisStep> Steps, SmtpCertificate? Certificate, IReadOnlyList<string> Capabilities, string Transcript);

public static class MailDiagnosisSteps
{
    public const string Connect = "connect", Ehlo = "ehlo", Tls = "tls", Certificate = "certificate", Auth = "auth", Sender = "sender", Recipient = "recipient", Reset = "reset";
    /// <summary>Pickup 傳輸只檢查目錄可寫入。</summary>
    public const string Directory = "directory";
    /// <summary>SMTP 診斷的步驟，依 StartTls 的執行順序。</summary>
    public static readonly IReadOnlyList<string> All = [Connect, Ehlo, Tls, Certificate, Auth, Sender, Recipient, Reset];
}

public static class MailDiagnosisStatuses
{
    public const string Passed = "passed", Warning = "warning", Failed = "failed", Skipped = "skipped";
}

/// <summary>
/// 以目前設定同步跑一次 SMTP 對話（<see cref="SmtpProbe"/>），逐步回報到哪一步失敗、為什麼；不寄出信、不經過 outbox，也不影響斷路器。
/// 每人每分鐘 5 次；寫行政稽核 <c>admin.mail_diagnose</c>（只記結果與失敗的步驟，不記收件地址與對話內容）。
/// </summary>
internal sealed partial class DiagnoseMailTransport(AuditTrail audit, IOptions<MailOptions> options, TimeProvider clock, ILogger<DiagnoseMailTransport> logger)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPost("", (MailDiagnosisRequest body, IRequestActor actor, DiagnoseMailTransport handler, CancellationToken ct) => handler.HandleAsync(actor.Id, body, ct).ToHttpResultAsync())
        .RequireRateLimiting(MailModule.DiagnosisRateLimit)
        .Audited(MailAudit.TransportDiagnosed)
        .WithName("DiagnoseMailTransport");

    public async Task<Result<MailDiagnosisDto>> HandleAsync(Guid actor, MailDiagnosisRequest request, CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.Enabled) return MailErrors.NotConfigured;
        var to = string.IsNullOrWhiteSpace(request.To) ? settings.From.Address : request.To;
        if (!MailAddresses.TryNormalize(to, out var recipient)) return MailErrors.AddressInvalid;
        if (!MailAddresses.Allowed(recipient, settings.AllowedRecipientDomains)) return MailErrors.RecipientBlocked;

        var startedAt = clock.GetUtcNow();
        var started = clock.GetTimestamp();
        using var probe = new SmtpProbe(settings.Smtp, clock);
        var (result, server) = settings.Transport == MailTransports.Smtp
            ? (await probe.RunAsync(settings.From.Address, recipient, ct), $"{settings.Smtp.Host}:{settings.Smtp.Port}（{settings.Smtp.Security}）")
            : (Pickup(settings.Pickup.Directory), "pickup");
        var elapsed = (long)clock.GetElapsedTime(started).TotalMilliseconds;
        var failed = result.Steps.FirstOrDefault(x => x.Status == MailDiagnosisStatuses.Failed)?.Id;

        // 診斷本身有跑完就是成功的操作；哪一步沒過記在明細。請求稽核寫入成功才回傳結果（失敗即拒絕）。
        audit.Describe(details: new { transport = settings.Transport, failedStep = failed, certificateVerdict = result.Certificate?.Verdict });
        LogDiagnosed(logger, failed ?? "none", result.Certificate?.Verdict ?? "none", elapsed);
        return new MailDiagnosisDto(failed is null, settings.Transport, server, startedAt, elapsed, result.Steps, result.Certificate, result.Capabilities, result.Transcript);
    }

    /// <summary>Pickup：建立並刪除一個暫存檔，確認應用程式集區身分可以寫入。</summary>
    private MailDiagnosisResult Pickup(string directory)
    {
        var at = clock.GetTimestamp();
        MailDiagnosisStep step;
        try
        {
            System.IO.Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, $".diagnose-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            step = new(MailDiagnosisSteps.Directory, MailDiagnosisStatuses.Passed, "目錄存在且可以寫入。", (long)clock.GetElapsedTime(at).TotalMilliseconds, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            step = new(MailDiagnosisSteps.Directory, MailDiagnosisStatuses.Failed, $"無法寫入 Pickup.Directory（{ex.GetType().Name}）：確認目錄存在，且應用程式集區身分有修改權限。",
                (long)clock.GetElapsedTime(at).TotalMilliseconds, null);
        }
        return new([step], null, [], "");
    }

    [LoggerMessage(EventId = 3308, EventName = "mail.diagnosed", Level = LogLevel.Information, Message = "Mail connection diagnosis finished (failed step {Stage}, certificate {Reason}) in {ElapsedMs} ms.")]
    private static partial void LogDiagnosed(ILogger logger, string stage, string reason, long elapsedMs);
}
