using System.Net;
using System.Net.Http.Json;
using AiNexus.Data.App.Mail;
using AiNexus.Features.Admin.Audit;
using AiNexus.Features.Admin.Mail;
using AiNexus.Features.Admin.Mail.Messages;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.MailFixtures;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.Mail;

public sealed class SendTestMailTests
{
    private const string Tests = "/api/v1/admin/mail-tests";

    [Fact]
    public async Task TestMailsAreQueuedLikeAnyOtherMailAndAuditedWithoutTheAddress()
    {
        await using var factory = new AppFactory(administrators: ["alice"]);
        using var admin = await factory.SignedInAsync();
        var response = await admin.PostAsJsonAsync(Tests, new MailTestRequest("ops@company.com"));
        response.EnsureSuccessStatusCode();
        var id = (await response.Content.ReadFromJsonAsync<MailEnqueuedDto>())!.MessageId;
        var message = await MessageAsync(factory, id);
        Assert.Equal(("mail.test", MailStates.Queued, "ops@company.com"), (message.Category, message.Status, message.RecipientAddress));
        Assert.NotNull(message.RequestedById);

        await Dispatcher(factory).DispatchAsync(CancellationToken.None);
        var sent = Assert.Single(factory.Mail.Sent);
        Assert.Contains("郵件設定測試", sent.Message.Subject, StringComparison.Ordinal);
        Assert.Contains("寄件地址：ainexus@fixture.test", sent.Message.TextBody, StringComparison.Ordinal);

        var audit = Assert.Single((await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=admin.mail_test"))!);
        Assert.DoesNotContain("ops@company.com", audit.DetailsJson, StringComparison.Ordinal);
        await AssertCodeAsync(await admin.PostAsJsonAsync(Tests, new MailTestRequest("Ops <ops@company.com>")), HttpStatusCode.BadRequest, MailErrors.AddressInvalid.Code);
        await AssertCodeAsync(await admin.PostAsJsonAsync(Tests, new MailTestRequest("")), HttpStatusCode.BadRequest, "validation_failed");
    }

    [Fact]
    public async Task DisabledTransportRefusesTestMails()
    {
        await using var factory = new AppFactory(administrators: ["alice"], services: services => services.PostConfigure<MailOptions>(x => x.Transport = MailTransports.Disabled));
        using var admin = await factory.SignedInAsync();
        await AssertCodeAsync(await admin.PostAsJsonAsync(Tests, new MailTestRequest("ops@company.com")), HttpStatusCode.ServiceUnavailable, MailErrors.NotConfigured.Code);
        var status = (await admin.GetFromJsonAsync<MailStatusDto>("/api/v1/admin/mail-status"))!;
        Assert.Equal((MailTransports.Disabled, false, (string?)null), (status.Transport, status.Enabled, status.Server));
    }
}
