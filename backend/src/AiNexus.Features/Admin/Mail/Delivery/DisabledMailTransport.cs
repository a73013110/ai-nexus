namespace AiNexus.Features.Admin.Mail.Delivery;

/// <summary><c>Mail:Transport=Disabled</c>：dispatcher 不認領，所以不應被開啟；被開啟代表程式錯誤。</summary>
public sealed class DisabledMailTransport : IMailTransport
{
    public string Name => MailTransports.Disabled;

    public Task<IMailSession> OpenAsync(CancellationToken ct) => throw new InvalidOperationException("Mail transport is disabled.");
}
