using System.Globalization;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace AiNexus.Features.Admin.Mail.Delivery;

/// <summary>
/// SMTP 伺服器憑證的摘要與判定（寄送與連線診斷共用）。<paramref name="Verdict"/> 見 <see cref="SmtpCertificateVerdicts"/>；
/// <paramref name="ChainStatus"/> 是 <see cref="X509ChainStatusFlags"/> 的名稱。
/// </summary>
public sealed record SmtpCertificate(string Subject, string Issuer, IReadOnlyList<string> DnsNames, DateTimeOffset NotBefore, DateTimeOffset NotAfter,
    string Sha256, string PolicyErrors, IReadOnlyList<string> ChainStatus, string Verdict)
{
    // 撤銷狀態查不到（內部 CA 沒有 CRL／AIA、CRL 伺服器離線）只代表「不知道」，不代表已撤銷：soft-fail，與瀏覽器一致。
    // 已撤銷（Revoked）、鏈不可信、名稱不符、過期仍然拒絕；那些情況要嘛修憑證，要嘛以指紋釘選。
    private const X509ChainStatusFlags Tolerated = X509ChainStatusFlags.RevocationStatusUnknown | X509ChainStatusFlags.OfflineRevocation;

    public bool Accepted => Verdict != SmtpCertificateVerdicts.Rejected;

    /// <summary>在 TLS 驗證回呼內呼叫：鏈在回呼結束後就會被釋放，所以當場複製需要的內容。</summary>
    public static SmtpCertificate Evaluate(X509Certificate? certificate, X509Chain? chain, SslPolicyErrors errors, string thumbprint)
    {
        var status = chain?.ChainStatus.Aggregate(X509ChainStatusFlags.NoError, (all, item) => all | item.Status) ?? X509ChainStatusFlags.NoError;
        var verdict = Judge(errors, status, certificate, thumbprint);
        var chainStatus = Enum.GetValues<X509ChainStatusFlags>().Where(x => x != X509ChainStatusFlags.NoError && status.HasFlag(x)).Select(x => x.ToString()).ToArray();
        if (certificate is null)
            return new("", "", [], default, default, "", errors.ToString(), chainStatus, verdict);
        // 回呼拿到的憑證屬於 SslStream，不能在這裡釋放；只有自己包出來的才釋放。
        using var created = certificate is X509Certificate2 ? null : new X509Certificate2(certificate);
        var x509 = created ?? (X509Certificate2)certificate;
        var names = x509.Extensions.OfType<X509SubjectAlternativeNameExtension>().SelectMany(x => x.EnumerateDnsNames()).Take(20).ToArray();
        return new(x509.Subject, x509.Issuer, names, new DateTimeOffset(x509.NotBefore.ToUniversalTime(), TimeSpan.Zero), new DateTimeOffset(x509.NotAfter.ToUniversalTime(), TimeSpan.Zero),
            Convert.ToHexString(x509.GetCertHash(HashAlgorithmName.SHA256)), errors.ToString(), chainStatus, verdict);
    }

    /// <summary>接受規則（<see cref="SmtpCertificateVerdicts"/>）：完全通過、只有撤銷狀態查不到、或指紋相符才接受。</summary>
    public static string Judge(SslPolicyErrors errors, X509ChainStatusFlags status, X509Certificate? certificate, string thumbprint)
        => errors == SslPolicyErrors.None ? SmtpCertificateVerdicts.Valid
            : errors == SslPolicyErrors.RemoteCertificateChainErrors && status != X509ChainStatusFlags.NoError && (status & ~Tolerated) == 0 ? SmtpCertificateVerdicts.RevocationUnknown
            : certificate is not null && thumbprint.Length > 0 && Pinned(certificate, thumbprint) ? SmtpCertificateVerdicts.Pinned
            : SmtpCertificateVerdicts.Rejected;

    /// <summary>寫進寄送嘗試與日誌的一行摘要（不含秘密）；拒絕原因放最前面，截斷時才不會先被切掉。</summary>
    public string Describe()
        => string.Join("; ", new[]
        {
            $"verdict={Verdict}",
            $"policy={PolicyErrors}",
            ChainStatus.Count > 0 ? $"chain={string.Join(",", ChainStatus)}" : null,
            Subject.Length > 0 ? $"subject={Subject}" : null,
            Issuer.Length > 0 ? $"issuer={Issuer}" : null,
            Subject.Length > 0 ? $"notAfter={NotAfter.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}" : null,
            Sha256.Length > 0 ? $"sha256={Sha256}" : null,
        }.OfType<string>());

    private static bool Pinned(X509Certificate certificate, string thumbprint)
    {
        var expected = Convert.FromHexString(thumbprint);
        var actual = expected.Length == 20 ? certificate.GetCertHash() : certificate.GetCertHash(HashAlgorithmName.SHA256);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}

/// <summary><see cref="SmtpCertificate.Verdict"/> 的值。</summary>
public static class SmtpCertificateVerdicts
{
    public const string Valid = "valid";
    /// <summary>只有撤銷狀態無法確認（soft-fail），其他都通過。</summary>
    public const string RevocationUnknown = "revocation_unknown";
    /// <summary>驗證不過，但指紋與 <c>ServerCertificateThumbprint</c> 相符。</summary>
    public const string Pinned = "pinned";
    public const string Rejected = "rejected";
}
