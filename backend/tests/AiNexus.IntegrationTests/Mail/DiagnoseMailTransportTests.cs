using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using AiNexus.Features.Admin.Audit;
using AiNexus.Features.Admin.Mail;
using AiNexus.Features.Admin.Mail.Transport;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.Mail;

public sealed class DiagnoseMailTransportTests
{
    private const string Diagnoses = "/api/v1/admin/mail-diagnoses";

    [Fact]
    public async Task AnUnreachableServerIsReportedStepByStepAndAuditedWithoutTheRecipient()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        await using var factory = new AppFactory(administrators: ["alice"], services: services => services.PostConfigure<MailOptions>(x =>
        {
            x.Smtp.Host = "127.0.0.1";
            x.Smtp.Port = port;
            x.Smtp.Security = MailSmtpOptions.NoSecurity;
            x.Smtp.TimeoutSeconds = 5;
        }));
        using var admin = await factory.SignedInAsync();

        var response = await admin.PostAsJsonAsync(Diagnoses, new MailDiagnosisRequest("ops@company.com"));
        response.EnsureSuccessStatusCode();
        var result = (await response.Content.ReadFromJsonAsync<MailDiagnosisDto>())!;
        Assert.False(result.Succeeded);
        Assert.Equal((MailTransports.Smtp, $"127.0.0.1:{port}（None）"), (result.Transport, result.Server));
        Assert.Equal((MailDiagnosisSteps.Connect, MailDiagnosisStatuses.Failed), (result.Steps[0].Id, result.Steps[0].Status));
        Assert.Equal(MailDiagnosisSteps.All.Count, result.Steps.Count);

        var audit = Assert.Single((await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=admin.mail_diagnose&result=failed"))!);
        Assert.Contains("\"failedStep\":\"connect\"", audit.DetailsJson, StringComparison.Ordinal);
        Assert.DoesNotContain("ops@company.com", audit.DetailsJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PickupChecksTheDirectoryAndRecipientsFollowTheSendingRules()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ainexus-mail-" + Guid.NewGuid().ToString("N"));
        await using var factory = new AppFactory(administrators: ["alice"], services: services => services.PostConfigure<MailOptions>(x =>
        {
            x.Transport = MailTransports.Pickup;
            x.Pickup.Directory = directory;
            x.AllowedRecipientDomains = ["company.com"];
        }));
        using var admin = await factory.SignedInAsync();
        try
        {
            var result = (await (await admin.PostAsJsonAsync(Diagnoses, new MailDiagnosisRequest("ops@company.com"))).Content.ReadFromJsonAsync<MailDiagnosisDto>())!;
            Assert.True(result.Succeeded);
            Assert.Equal((MailDiagnosisSteps.Directory, MailDiagnosisStatuses.Passed), (Assert.Single(result.Steps).Id, result.Steps[0].Status));
            Assert.Empty(Directory.EnumerateFiles(directory));

            await AssertCodeAsync(await admin.PostAsJsonAsync(Diagnoses, new MailDiagnosisRequest("ops@other.com")), HttpStatusCode.BadRequest, MailErrors.RecipientBlocked.Code);
            await AssertCodeAsync(await admin.PostAsJsonAsync(Diagnoses, new MailDiagnosisRequest("Ops <ops@company.com>")), HttpStatusCode.BadRequest, MailErrors.AddressInvalid.Code);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task DisabledTransportCannotBeDiagnosedAndMembersAreForbidden()
    {
        await using var factory = new AppFactory(administrators: ["alice"], services: services => services.PostConfigure<MailOptions>(x => x.Transport = MailTransports.Disabled));
        using var admin = await factory.SignedInAsync();
        await AssertCodeAsync(await admin.PostAsJsonAsync(Diagnoses, new MailDiagnosisRequest(null)), HttpStatusCode.ServiceUnavailable, MailErrors.NotConfigured.Code);
        using var member = await factory.SignedInAsync("bob");
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PostAsJsonAsync(Diagnoses, new MailDiagnosisRequest(null))).StatusCode);
    }
}
