using AiNexus.Data.App.Mail;

namespace AiNexus.Features.Admin.Mail.Delivery;

/// <summary>
/// 傳輸在哪一步失敗（<see cref="MailAttemptStages"/>）與當時的 TLS 憑證摘要；原始例外在 <see cref="Exception.InnerException"/>，
/// 分類仍依原始例外（<see cref="MailFailures.Classify"/>）。
/// </summary>
public sealed class MailSendException(string stage, SmtpCertificate? certificate, Exception inner) : Exception(inner.Message, inner)
{
    public string Stage { get; } = stage;
    public SmtpCertificate? Certificate { get; } = certificate;
}
