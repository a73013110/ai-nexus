using MimeKit;

namespace AiNexus.Features.Admin.Mail.Delivery;

/// <summary>
/// 寄送管道（SMTP、pickup 目錄）。<see cref="MailDispatcher"/> 每一輪為每條 lane 開一個 session，連續寄多封後關閉；
/// 失敗以例外表示，由 <see cref="MailFailures"/> 分類成暫時性、永久性或傳輸層故障。
/// </summary>
public interface IMailTransport
{
    /// <summary>寫進紀錄的傳輸名稱（<see cref="MailTransports"/>）。</summary>
    string Name { get; }

    Task<IMailSession> OpenAsync(CancellationToken ct);
}

/// <summary>一條連線（或一次 pickup 寫入的範圍）；連線在第一次寄送時才建立，斷線後下一封重新連線。</summary>
public interface IMailSession : IAsyncDisposable
{
    /// <summary>寄出並回傳伺服器的最終回應（例：<c>250 2.0.0 OK</c>）；回傳代表 relay 已接受，不代表已投遞。</summary>
    Task<string> SendAsync(OutgoingMail mail, CancellationToken ct);
}

/// <summary>一封要寄出的信：outbox 的 Id（pickup 檔名用）與組好的 MIME。</summary>
public sealed record OutgoingMail(Guid Id, MimeMessage Message);
