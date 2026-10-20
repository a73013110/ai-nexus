using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using AiNexus.Features.Admin.Mail.Delivery;
using AiNexus.UnitTests.Support;

namespace AiNexus.UnitTests.Mail;

public sealed class SmtpCertificateTests
{
    private const SslPolicyErrors Chain = SslPolicyErrors.RemoteCertificateChainErrors;

    [Theory]
    [InlineData(SslPolicyErrors.None, X509ChainStatusFlags.NoError, SmtpCertificateVerdicts.Valid)]
    [InlineData(Chain, X509ChainStatusFlags.RevocationStatusUnknown | X509ChainStatusFlags.OfflineRevocation, SmtpCertificateVerdicts.RevocationUnknown)]
    [InlineData(Chain, X509ChainStatusFlags.RevocationStatusUnknown, SmtpCertificateVerdicts.RevocationUnknown)]
    [InlineData(Chain, X509ChainStatusFlags.Revoked | X509ChainStatusFlags.RevocationStatusUnknown, SmtpCertificateVerdicts.Rejected)]
    [InlineData(Chain, X509ChainStatusFlags.UntrustedRoot | X509ChainStatusFlags.RevocationStatusUnknown, SmtpCertificateVerdicts.Rejected)]
    [InlineData(Chain, X509ChainStatusFlags.NotTimeValid, SmtpCertificateVerdicts.Rejected)]
    [InlineData(Chain | SslPolicyErrors.RemoteCertificateNameMismatch, X509ChainStatusFlags.RevocationStatusUnknown, SmtpCertificateVerdicts.Rejected)]
    [InlineData(SslPolicyErrors.RemoteCertificateNotAvailable, X509ChainStatusFlags.NoError, SmtpCertificateVerdicts.Rejected)]
    public void OnlyAnUnknownRevocationStatusIsToleratedWithoutAPin(SslPolicyErrors errors, X509ChainStatusFlags status, string verdict)
        => Assert.Equal(verdict, SmtpCertificate.Judge(errors, status, null, ""));

    [Fact]
    public void APinnedThumbprintAcceptsAnUntrustedCertificateOnlyWhenItMatches()
    {
        using var certificate = FakeSmtpServer.SelfSigned();
        var sha256 = Convert.ToHexString(certificate.GetCertHash(HashAlgorithmName.SHA256));
        Assert.Equal(SmtpCertificateVerdicts.Pinned, SmtpCertificate.Judge(Chain, X509ChainStatusFlags.UntrustedRoot, certificate, sha256));
        Assert.Equal(SmtpCertificateVerdicts.Pinned, SmtpCertificate.Judge(Chain, X509ChainStatusFlags.UntrustedRoot, certificate, certificate.Thumbprint));
        Assert.Equal(SmtpCertificateVerdicts.Rejected, SmtpCertificate.Judge(Chain, X509ChainStatusFlags.UntrustedRoot, certificate, new string('A', 64)));
    }

    [Fact]
    public void AnInternalCaWithoutRevocationInformationIsAccepted()
    {
        // 與公司內部 CA 同樣的情況：根受信任，但簽發的憑證沒有 CRL／AIA，撤銷狀態查不到。
        var now = DateTimeOffset.UtcNow;
        using var caKey = RSA.Create(2048);
        var caRequest = new CertificateRequest("CN=Internal Test CA", caKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        caRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        caRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        using var ca = caRequest.CreateSelfSigned(now.AddDays(-1), now.AddYears(1));
        using var leafKey = RSA.Create(2048);
        var leafRequest = new CertificateRequest("CN=*.company.test", leafKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var leaf = leafRequest.Create(ca, now.AddDays(-1), now.AddDays(90), [1, 2, 3, 4]);
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(ca);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
        chain.ChainPolicy.UrlRetrievalTimeout = TimeSpan.FromSeconds(1);
        Assert.False(chain.Build(leaf));

        var summary = SmtpCertificate.Evaluate(leaf, chain, Chain, "");
        Assert.Equal(SmtpCertificateVerdicts.RevocationUnknown, summary.Verdict);
        Assert.True(summary.Accepted);
        Assert.Contains(nameof(X509ChainStatusFlags.RevocationStatusUnknown), summary.ChainStatus);
        Assert.Equal(("CN=*.company.test", "CN=Internal Test CA"), (summary.Subject, summary.Issuer));
    }

    [Fact]
    public void TheSummaryCarriesWhatAnAdministratorNeedsToPinOrReplaceTheCertificate()
    {
        using var certificate = FakeSmtpServer.SelfSigned();
        var summary = SmtpCertificate.Evaluate(certificate, null, Chain, "");
        Assert.Equal(("CN=localhost", false), (summary.Subject, summary.Accepted));
        Assert.Contains("localhost", summary.DnsNames);
        Assert.Equal(Convert.ToHexString(certificate.GetCertHash(HashAlgorithmName.SHA256)), summary.Sha256);
        Assert.StartsWith("verdict=rejected; policy=RemoteCertificateChainErrors", summary.Describe(), StringComparison.Ordinal);
        Assert.Contains($"sha256={summary.Sha256}", summary.Describe(), StringComparison.Ordinal);
    }
}
