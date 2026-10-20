using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace AiNexus.UnitTests.Support;

/// <summary>
/// 只聽 loopback 的最小 SMTP 伺服器：EHLO、STARTTLS（給了 <see cref="Certificate"/> 才提供）、AUTH LOGIN／PLAIN、MAIL、RCPT、DATA、RSET、QUIT。
/// 一次只服務一條連線就夠測試用；收到的指令與內容留在 <see cref="Commands"/>、<see cref="Messages"/>。
/// </summary>
public sealed class FakeSmtpServer : IAsyncDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource stop = new();
    private readonly Task loop;

    public FakeSmtpServer(X509Certificate2? certificate = null)
    {
        Certificate = certificate;
        listener.Start();
        loop = AcceptAsync();
    }

    public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;
    public X509Certificate2? Certificate { get; }
    public bool AcceptAuth { get; set; } = true;
    /// <summary>依 RCPT 地址回覆；null 表示接受。</summary>
    public Func<string, string?>? RecipientReply { get; set; }
    public ConcurrentQueue<string> Commands { get; } = new();
    public ConcurrentQueue<string> Messages { get; } = new();

    /// <summary>自簽憑證（CN=localhost，SAN 含 127.0.0.1）；用戶端不信任它的根。</summary>
    public static X509Certificate2 SelfSigned(DateTimeOffset? notAfter = null)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName("localhost");
        names.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), notAfter ?? DateTimeOffset.UtcNow.AddDays(90));
        // Windows 的 SChannel 不接受暫時金鑰，所以經 PFX 匯出再載入。
        return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pfx), null);
    }

    public async ValueTask DisposeAsync()
    {
        await stop.CancelAsync();
        listener.Stop();
        try { await loop; } catch (OperationCanceledException) { } catch (SocketException) { } catch (ObjectDisposedException) { }
        stop.Dispose();
    }

    private async Task AcceptAsync()
    {
        while (!stop.IsCancellationRequested)
        {
            var client = await listener.AcceptTcpClientAsync(stop.Token);
            _ = Task.Run(() => SessionAsync(client));
        }
    }

    private async Task SessionAsync(TcpClient client)
    {
        using var tcp = client;
        Stream stream = tcp.GetStream();
        var tls = false;
        try
        {
            var (reader, writer) = Open(stream);
            await writer.WriteLineAsync("220 fake.test ESMTP");
            while (await reader.ReadLineAsync() is { } line)
            {
                Commands.Enqueue(line);
                var verb = line.Split(' ', 2)[0].ToUpperInvariant();
                switch (verb)
                {
                    case "EHLO":
                        await writer.WriteLineAsync("250-fake.test");
                        if (Certificate is not null && !tls) await writer.WriteLineAsync("250-STARTTLS");
                        if (Certificate is null || tls) await writer.WriteLineAsync("250-AUTH LOGIN PLAIN");
                        await writer.WriteLineAsync("250 SIZE 1048576");
                        break;
                    case "STARTTLS":
                        await writer.WriteLineAsync("220 2.0.0 Ready to start TLS");
                        var ssl = new SslStream(stream);
                        await ssl.AuthenticateAsServerAsync(Certificate!);
                        stream = ssl;
                        tls = true;
                        (reader, writer) = Open(stream);
                        break;
                    case "AUTH":
                        if (line.StartsWith("AUTH LOGIN", StringComparison.OrdinalIgnoreCase))
                        {
                            await writer.WriteLineAsync("334 VXNlcm5hbWU6");
                            Commands.Enqueue(await reader.ReadLineAsync() ?? "");
                            await writer.WriteLineAsync("334 UGFzc3dvcmQ6");
                            Commands.Enqueue(await reader.ReadLineAsync() ?? "");
                        }
                        await writer.WriteLineAsync(AcceptAuth ? "235 2.7.0 Authentication successful" : "535 5.7.3 Authentication unsuccessful");
                        break;
                    case "MAIL":
                        await writer.WriteLineAsync("250 2.1.0 Sender OK");
                        break;
                    case "RCPT":
                        var address = line[(line.IndexOf('<', StringComparison.Ordinal) + 1)..].TrimEnd('>');
                        await writer.WriteLineAsync(RecipientReply?.Invoke(address) ?? "250 2.1.5 Recipient OK");
                        break;
                    case "DATA":
                        await writer.WriteLineAsync("354 Start mail input; end with <CRLF>.<CRLF>");
                        var body = new StringBuilder();
                        while (await reader.ReadLineAsync() is { } data && data != ".") body.AppendLine(data);
                        Messages.Enqueue(body.ToString());
                        await writer.WriteLineAsync("250 2.6.0 Queued mail for delivery");
                        break;
                    case "RSET":
                        await writer.WriteLineAsync("250 2.0.0 Resetting");
                        break;
                    case "QUIT":
                        await writer.WriteLineAsync("221 2.0.0 Bye");
                        return;
                    default:
                        await writer.WriteLineAsync("502 5.5.2 Command not recognized");
                        break;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or AuthenticationException or ObjectDisposedException)
        {
            // 用戶端拒絕憑證或中途斷線。
        }
    }

    private static (StreamReader, StreamWriter) Open(Stream stream)
        => (new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true), new StreamWriter(stream, Encoding.ASCII, 1024, leaveOpen: true) { NewLine = "\r\n", AutoFlush = true });
}
