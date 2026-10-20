using System.Globalization;
using System.Net.Sockets;
using AiNexus.Features.Admin.Mail.Delivery;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace AiNexus.Features.Admin.Mail.Transport;

/// <summary>
/// 連線診斷的 SMTP 對話：TCP → EHLO → STARTTLS／TLS → 憑證 → AUTH → MAIL FROM → RCPT TO → RSET，最後 QUIT，不送 DATA，所以不會寄出信。
/// 連線設定與憑證規則和寄送共用（<see cref="SmtpConnector"/>），診斷通過就代表寄送時會走同一條路。某一步失敗後，之後的步驟標為略過。
/// </summary>
public sealed class SmtpProbe(MailSmtpOptions smtp, TimeProvider clock) : IDisposable
{
    private readonly List<MailDiagnosisStep> steps = [];
    private readonly MailTranscript transcript = new();
    private SmtpCertificate? certificate;
    private string[] capabilities = [];

    public async Task<MailDiagnosisResult> RunAsync(string sender, string recipient, CancellationToken ct)
    {
        using var client = SmtpConnector.Configure(new ProbeClient(transcript), smtp, observed => certificate = observed);
        // 每一步各有 MailKit 的 Timeout；整體再加一個上限，避免 DNS 或 TCP 卡住整個請求。
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(smtp.TimeoutSeconds * 2), clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, deadline.Token);
        try
        {
            await ProbeAsync(client, sender, recipient, linked.Token);
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            var next = MailDiagnosisSteps.All.First(id => steps.All(x => x.Id != id));
            Fail(next, $"超過 {smtp.TimeoutSeconds * 2} 秒仍未完成，已中止。", clock.GetTimestamp());
        }
        finally
        {
            if (client.IsConnected)
            {
                try { await client.DisconnectAsync(quit: true, CancellationToken.None); }
                catch (Exception ex) when (ex is IOException or SmtpProtocolException or OperationCanceledException) { }
            }
        }
        return new(Skip(), certificate, capabilities, transcript.ToString());
    }

    private async Task ProbeAsync(ProbeClient client, string sender, string recipient, CancellationToken ct)
    {
        var at = clock.GetTimestamp();
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        try
        {
            await socket.ConnectAsync(smtp.Host, smtp.Port, ct);
        }
        catch (SocketException ex)
        {
            socket.Dispose();
            Fail(MailDiagnosisSteps.Connect, $"無法連線 {smtp.Host}:{smtp.Port}（{ex.SocketErrorCode}）：{ex.Message}", at);
            return;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
        Pass(MailDiagnosisSteps.Connect, $"已連線 {socket.RemoteEndPoint}。", at);

        at = clock.GetTimestamp();
        var security = SmtpConnector.Security(smtp.Security);
        try
        {
            // MailKit 接手 socket：問候、EHLO、STARTTLS（或一連上就 TLS）與 TLS 後的 EHLO 都在這一步。
            await client.ConnectAsync(socket, smtp.Host, smtp.Port, security, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            socket.Dispose();
            Handshake(security, client, ex, at);
            return;
        }
        Handshake(security, client, null, at);
        // 斷線後 MailKit 會清掉能力，所以連上就先記下。
        capabilities = Capabilities(client);

        at = clock.GetTimestamp();
        if (smtp.UserName.Length == 0)
        {
            Add(MailDiagnosisSteps.Auth, MailDiagnosisStatuses.Skipped, "未設定 UserName，以匿名 relay 寄送（由 mail server 依主機 IP 允許）。", at);
        }
        else if (!client.Capabilities.HasFlag(SmtpCapabilities.Authentication))
        {
            Fail(MailDiagnosisSteps.Auth, "伺服器沒有提供 AUTH：確認 mail server 已啟用基本驗證，且只在加密連線後提供時 Security 要用 StartTls。", at);
            return;
        }
        else
        {
            try
            {
                await client.AuthenticateAsync(smtp.UserName, smtp.Password, ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                Fail(MailDiagnosisSteps.Auth, $"{ex.Message}（伺服器支援：{string.Join("、", client.AuthenticationMechanisms)}）", at);
                return;
            }
            Pass(MailDiagnosisSteps.Auth, $"以 {smtp.UserName} 通過 {transcript.Mechanism ?? "SASL"} 驗證。", at);
        }

        if (!await CommandAsync(client, MailDiagnosisSteps.Sender, $"MAIL FROM:<{sender}>", ct)) return;
        if (!await CommandAsync(client, MailDiagnosisSteps.Recipient, $"RCPT TO:<{recipient}>", ct)) return;
        await CommandAsync(client, MailDiagnosisSteps.Reset, "RSET", ct);
    }

    /// <summary>連線階段的結果拆成 EHLO、TLS 與憑證三步；順序依 Security（SslOnConnect 先 TLS 再 EHLO）。</summary>
    private void Handshake(SecureSocketOptions security, ProbeClient client, Exception? error, long at)
    {
        if (security == SecureSocketOptions.None)
        {
            if (error is null) Pass(MailDiagnosisSteps.Ehlo, "伺服器問候與 EHLO 成功。", at);
            else Fail(MailDiagnosisSteps.Ehlo, $"{error.GetType().Name}：{error.Message}", at);
            Add(MailDiagnosisSteps.Tls, MailDiagnosisStatuses.Skipped, "Security=None：未加密（只允許 loopback 主機）。", at);
            Add(MailDiagnosisSteps.Certificate, MailDiagnosisStatuses.Skipped, "未使用 TLS，沒有憑證。", at);
            return;
        }
        var failed = error is null ? null : FailedStep(security, error);
        string[] order = security == SecureSocketOptions.SslOnConnect ? [MailDiagnosisSteps.Tls, MailDiagnosisSteps.Ehlo] : [MailDiagnosisSteps.Ehlo, MailDiagnosisSteps.Tls];
        foreach (var step in order)
        {
            if (step == failed)
            {
                Fail(step, step == MailDiagnosisSteps.Tls ? TlsFailure(error!) : $"{error!.GetType().Name}：{error.Message}", at);
                if (step == MailDiagnosisSteps.Tls && certificate is not null) Certificate();
                return;
            }
            if (step == MailDiagnosisSteps.Ehlo) Pass(step, "伺服器問候與 EHLO 成功。", at);
            else
            {
                Pass(step, $"TLS 已建立（{client.SslProtocol}）。", at);
                Certificate();
            }
        }
    }

    // 驗證回呼有沒有被叫到、有沒有送出 STARTTLS，決定失敗在 TLS 還是問候／EHLO。
    private string FailedStep(SecureSocketOptions security, Exception error)
    {
        if (error is SslHandshakeException) return MailDiagnosisSteps.Tls;
        if (security == SecureSocketOptions.SslOnConnect) return certificate is null ? MailDiagnosisSteps.Tls : MailDiagnosisSteps.Ehlo;
        if (error is NotSupportedException) return MailDiagnosisSteps.Tls;
        if (transcript.StartTlsSent) return certificate is { Accepted: true } ? MailDiagnosisSteps.Ehlo : MailDiagnosisSteps.Tls;
        return MailDiagnosisSteps.Ehlo;
    }

    private string TlsFailure(Exception error)
        => error is NotSupportedException ? "伺服器沒有提供 STARTTLS：改用 SslOnConnect（通常是 465），或請 mail server 啟用 TLS。"
            : certificate is { Accepted: false } ? "憑證不被接受，原因見下一步。"
            : $"TLS 交握失敗（{error.GetType().Name}）：{error.InnerException?.Message ?? error.Message}";

    private void Certificate()
    {
        var current = certificate!;
        var at = clock.GetTimestamp();
        var identity = current.Subject.Length > 0
            ? $"{current.Subject}，由 {current.Issuer} 簽發，{current.NotAfter.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} 到期"
            : "伺服器沒有提供憑證";
        var problems = string.Join("、", current.ChainStatus.Prepend(current.PolicyErrors));
        switch (current.Verdict)
        {
            case SmtpCertificateVerdicts.Valid:
                var expiring = current.NotAfter - clock.GetUtcNow() < TimeSpan.FromDays(30);
                Add(MailDiagnosisSteps.Certificate, expiring ? MailDiagnosisStatuses.Warning : MailDiagnosisStatuses.Passed,
                    expiring ? $"{identity}；30 天內到期，記得換憑證（有釘選指紋時要一併更新）。" : $"{identity}；驗證通過。", at);
                break;
            case SmtpCertificateVerdicts.RevocationUnknown:
                Add(MailDiagnosisSteps.Certificate, MailDiagnosisStatuses.Warning,
                    $"{identity}；撤銷狀態無法確認（簽發者沒有提供 CRL／OCSP，或連不到），其餘驗證通過，已容忍。", at);
                break;
            case SmtpCertificateVerdicts.Pinned:
                Add(MailDiagnosisSteps.Certificate, MailDiagnosisStatuses.Warning,
                    $"{identity}；驗證不過（{problems}），但指紋與 ServerCertificateThumbprint 相符，換憑證後要更新指紋。", at);
                break;
            default:
                Add(MailDiagnosisSteps.Certificate, MailDiagnosisStatuses.Failed,
                    $"{identity}；不被接受（{problems}）。名稱不符要改 Host；自簽或內部 CA 未受信任時，可把 SHA-256 指紋設為 ServerCertificateThumbprint。", at);
                break;
        }
    }

    private async Task<bool> CommandAsync(ProbeClient client, string step, string command, CancellationToken ct)
    {
        var at = clock.GetTimestamp();
        SmtpResponse response;
        try
        {
            response = await client.CommandAsync(command, ct);
        }
        catch (SmtpCommandException ex)
        {
            Fail(step, $"{(int)ex.StatusCode} {ex.Message}", at, (int)ex.StatusCode);
            return false;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // 伺服器中途斷線或協定錯誤：照樣回報在這一步，不讓整個診斷變成 500。
            Fail(step, $"{ex.GetType().Name}：{ex.Message}", at);
            return false;
        }
        var status = (int)response.StatusCode;
        var text = $"{status} {response.Response}";
        if (status is >= 200 and < 300)
        {
            Add(step, MailDiagnosisStatuses.Passed, text, at, status);
            return true;
        }
        Fail(step, text, at, status);
        return false;
    }

    /// <summary>失敗之後沒有執行的步驟補成略過，讓畫面一律顯示完整流程。</summary>
    private List<MailDiagnosisStep> Skip()
    {
        foreach (var step in MailDiagnosisSteps.All.Where(id => steps.All(x => x.Id != id)).ToList())
            steps.Add(new(step, MailDiagnosisStatuses.Skipped, "前一步失敗，未執行。", 0, null));
        return steps;
    }

    private static string[] Capabilities(SmtpClient client)
    {
        var flags = Enum.GetValues<SmtpCapabilities>().Where(x => x != SmtpCapabilities.None && client.Capabilities.HasFlag(x))
            .Select(x => x == SmtpCapabilities.Size && client.MaxSize > 0 ? $"SIZE {client.MaxSize}" : x.ToString().ToUpperInvariant());
        var mechanisms = client.AuthenticationMechanisms.Count > 0 ? [$"AUTH {string.Join(" ", client.AuthenticationMechanisms)}"] : Array.Empty<string>();
        return [.. flags, .. mechanisms];
    }

    public void Dispose() => transcript.Dispose();

    private void Pass(string step, string summary, long at) => Add(step, MailDiagnosisStatuses.Passed, summary, at);

    private void Fail(string step, string summary, long at, int? status = null) => Add(step, MailDiagnosisStatuses.Failed, summary, at, status);

    private void Add(string step, string status, string summary, long at, int? smtpStatus = null)
        => steps.Add(new(step, status, summary, (long)clock.GetElapsedTime(at).TotalMilliseconds, smtpStatus));

    /// <summary>MailKit 的 <c>SendCommandAsync</c> 是 protected：單獨送 MAIL FROM／RCPT TO／RSET 只能透過子類別。</summary>
    private sealed class ProbeClient(MailTranscript transcript) : SmtpClient(transcript)
    {
        public Task<SmtpResponse> CommandAsync(string command, CancellationToken ct) => SendCommandAsync(command, ct);
    }
}

/// <summary>SMTP 診斷的結果（不含時間與傳輸資訊，由呼叫端補上）。</summary>
public sealed record MailDiagnosisResult(IReadOnlyList<MailDiagnosisStep> Steps, SmtpCertificate? Certificate, IReadOnlyList<string> Capabilities, string Transcript);
