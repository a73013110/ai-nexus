using System.Collections.Concurrent;
using AiNexus.Features.Admin.Mail;
using AiNexus.Features.Admin.Mail.Delivery;

namespace AiNexus.IntegrationTests.Support;

/// <summary>
/// 取代 SMTP 的測試傳輸：記下寄出的信；<see cref="Failure"/> 回傳例外時模擬伺服器拒絕或斷線（每封呼叫一次）。
/// </summary>
public sealed class RecordingMailTransport : IMailTransport
{
    public ConcurrentQueue<OutgoingMail> Sent { get; } = new();

    /// <summary>回傳 null 表示寄出成功。</summary>
    public Func<OutgoingMail, Exception?>? Failure { get; set; }

    public int Sessions => sessions;
    private int sessions;

    public string Name => MailTransports.Smtp;

    public Task<IMailSession> OpenAsync(CancellationToken ct)
    {
        Interlocked.Increment(ref sessions);
        return Task.FromResult<IMailSession>(new Session(this));
    }

    private sealed class Session(RecordingMailTransport transport) : IMailSession
    {
        public Task<string> SendAsync(OutgoingMail mail, CancellationToken ct)
        {
            if (transport.Failure?.Invoke(mail) is { } failure) throw failure;
            transport.Sent.Enqueue(mail);
            return Task.FromResult($"250 2.0.0 OK queued as {mail.Id:N} for fixture@example.test");
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
