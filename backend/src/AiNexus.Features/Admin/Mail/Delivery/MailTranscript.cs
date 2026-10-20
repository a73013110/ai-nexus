using System.Text;
using MailKit;

namespace AiNexus.Features.Admin.Mail.Delivery;

/// <summary>
/// 連線診斷的 SMTP 對話紀錄（MailKit 的 protocol logger）。<c>AUTH</c> 之後到伺服器回非 334 為止，用戶端送出的內容一律遮罩：
/// 不依賴 MailKit 的秘密偵測，AUTH LOGIN 的帳號與密碼（各一行 base64）、PLAIN 的初始回應都不會出現。只存在記憶體、只回給發起診斷的人。
/// </summary>
public sealed class MailTranscript : IProtocolLogger
{
    public const int MaxLength = 16 * 1024;
    private const string Masked = "********";

    private readonly StringBuilder text = new();
    private bool authenticating;

    public IAuthenticationSecretDetector? AuthenticationSecretDetector { get; set; }

    /// <summary>用戶端是否已送出 STARTTLS（用來判斷失敗發生在 EHLO 還是 TLS）。</summary>
    public bool StartTlsSent { get; private set; }

    /// <summary>用戶端送出的 AUTH 機制（例：LOGIN）；未驗證為 null。</summary>
    public string? Mechanism { get; private set; }

    public void LogConnect(Uri uri) => Append('*', $"connected to {uri.Host}:{uri.Port}\r\n");

    public void LogClient(byte[] buffer, int offset, int count)
    {
        var line = Encoding.ASCII.GetString(buffer, offset, count);
        if (authenticating)
        {
            Append('C', Masked + "\r\n");
            return;
        }
        if (line.StartsWith("AUTH ", StringComparison.OrdinalIgnoreCase))
        {
            authenticating = true;
            var parts = line.TrimEnd('\r', '\n').Split(' ', 3);
            Mechanism = parts.Length > 1 ? parts[1].ToUpperInvariant() : null;
            Append('C', $"AUTH {Mechanism}{(parts.Length > 2 ? " " + Masked : "")}\r\n");
            return;
        }
        if (line.StartsWith("STARTTLS", StringComparison.OrdinalIgnoreCase)) StartTlsSent = true;
        Append('C', line);
    }

    public void LogServer(byte[] buffer, int offset, int count)
    {
        var line = Encoding.ASCII.GetString(buffer, offset, count);
        // 334 是要用戶端繼續送驗證資料；其他回應代表驗證結束。
        if (authenticating && !line.StartsWith("334", StringComparison.Ordinal)) authenticating = false;
        Append('S', line);
    }

    public override string ToString() => text.ToString();

    public void Dispose() { }

    // MailKit 幾乎都以整行呼叫；偶爾一行被拆成兩次時只是多一個前綴，不影響閱讀。
    private void Append(char from, string value)
    {
        foreach (var line in value.Split('\n'))
        {
            var trimmed = line.TrimEnd('\r');
            if (trimmed.Length == 0 || text.Length >= MaxLength) continue;
            text.Append(from).Append(": ").Append(trimmed).Append('\n');
        }
    }
}
