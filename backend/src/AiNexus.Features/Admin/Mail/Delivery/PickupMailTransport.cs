using System.Globalization;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Mail.Delivery;

/// <summary>
/// 把信寫成 <c>&lt;Directory&gt;/&lt;yyyyMMdd&gt;/&lt;Id&gt;.eml</c>，不實際寄出：開發用，也可以指向 IIS SMTP 服務的 pickup 目錄。
/// 先寫暫存檔再改名，pickup 服務不會讀到寫一半的檔案。
/// </summary>
public sealed class PickupMailTransport(IOptions<MailOptions> options, TimeProvider clock) : IMailTransport
{
    public string Name => MailTransports.Pickup;

    public Task<IMailSession> OpenAsync(CancellationToken ct) => Task.FromResult<IMailSession>(new Session(options.Value.Pickup.Directory, clock));

    private sealed class Session(string root, TimeProvider clock) : IMailSession
    {
        public async Task<string> SendAsync(OutgoingMail mail, CancellationToken ct)
        {
            var day = clock.GetUtcNow().ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            var directory = Path.Combine(root, day);
            Directory.CreateDirectory(directory);
            var name = $"{mail.Id:N}.eml";
            var temporary = Path.Combine(directory, name + ".tmp");
            await mail.Message.WriteToAsync(temporary, ct);
            File.Move(temporary, Path.Combine(directory, name), overwrite: true);
            return $"pickup:{day}/{name}";
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
