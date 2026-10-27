using System.Net;
using System.Net.Http.Json;
using AiNexus.Data.App;
using AiNexus.Data.App.Mail;
using AiNexus.Features.Admin.Audit;
using AiNexus.Features.Admin.Mail;
using AiNexus.Features.Admin.Mail.Messages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.MailFixtures;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.Mail;

public sealed class SendTestMailTests
{
    private const string Tests = "/api/v1/admin/mail-tests";
    private const string Samples = "/api/v1/admin/mail-samples";

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

        var audit = Assert.Single((await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=mail.test_message.sent"))!);
        Assert.DoesNotContain("ops@company.com", audit.DetailsJson, StringComparison.Ordinal);
        await AssertCodeAsync(await admin.PostAsJsonAsync(Tests, new MailTestRequest("Ops <ops@company.com>")), HttpStatusCode.BadRequest, MailErrors.AddressInvalid.Code);
        await AssertCodeAsync(await admin.PostAsJsonAsync(Tests, new MailTestRequest("")), HttpStatusCode.BadRequest, "validation_failed");
    }

    [Fact]
    public async Task EveryRegisteredMailIsListedWithParametersNamedAfterTheModelConstructor()
    {
        await using var factory = new AppFactory(administrators: ["alice"]);
        using var admin = await factory.SignedInAsync();
        var listed = (await admin.GetFromJsonAsync<MailSampleDto[]>(Samples))!;
        Assert.Equal(["mail.test", "account.email-change-code", "account.reauthentication-code", "account.security-notice"], listed.Select(x => x.Category));
        Assert.Equal(("EmailChangeCodeMail", "VerificationCodeMailModel", "security", true), (listed[1].TemplateType, listed[1].ModelType, listed[1].Priority, listed[1].Sensitive));
        // 範例值不帶操作者或任何使用者的資料，人名與信箱一律虛構。
        Assert.All(listed.SelectMany(x => x.Parameters).Where(x => x.Name.EndsWith("Name", StringComparison.Ordinal) || x.Name == "RequestedBy"),
            x => Assert.Equal(MailSampleContext.ExampleName, x.Example));
        Assert.DoesNotContain(listed.SelectMany(x => x.Parameters), x => x.Example?.Contains("alice", StringComparison.Ordinal) == true);

        // 參數名必須與模型建構子一致，管理頁顯示的 C# 用法才能照抄。
        foreach (var sample in factory.Services.GetServices<MailSample>())
        {
            var model = sample.GetType().GetGenericArguments().Single();
            var constructor = model.GetConstructors().Single();
            var described = listed.Single(x => x.Category == sample.Category).Parameters;
            Assert.Equal(constructor.GetParameters().Select(x => x.Name), described.Select(x => x.Name));
        }
    }

    [Fact]
    public async Task RegisteredMailsAreSentWithTheGivenParametersAndAMarkedSubject()
    {
        await using var factory = new AppFactory(administrators: ["alice"]);
        using var admin = await factory.SignedInAsync();
        var response = await admin.PostAsJsonAsync(Tests, new MailTestRequest("ops@company.com", "account.email-change-code",
            new Dictionary<string, string?> { ["Code"] = "654321", ["Minutes"] = "7" }));
        response.EnsureSuccessStatusCode();
        var id = (await response.Content.ReadFromJsonAsync<MailEnqueuedDto>())!.MessageId;
        var message = await MessageAsync(factory, id);
        Assert.Equal(("account.email-change-code", MailPriority.Security, true), (message.Category, message.Priority, message.Sensitive));

        await Dispatcher(factory).DispatchAsync(CancellationToken.None);
        var sent = Assert.Single(factory.Mail.Sent);
        Assert.Equal("[測試] 確認你的通知信箱", sent.Message.Subject);
        Assert.Contains("654321", sent.Message.TextBody, StringComparison.Ordinal);
        Assert.Contains("驗證碼 7 分鐘內有效", sent.Message.TextBody, StringComparison.Ordinal);
        Assert.Contains($"{MailSampleContext.ExampleName} 您好", sent.Message.TextBody, StringComparison.Ordinal);

        var audit = Assert.Single((await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=mail.test_message.sent"))!);
        Assert.DoesNotContain("654321", audit.DetailsJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownMailsAndInvalidParametersAreRejected()
    {
        await using var factory = new AppFactory(administrators: ["alice"]);
        using var admin = await factory.SignedInAsync();
        await AssertCodeAsync(await admin.PostAsJsonAsync(Tests, new MailTestRequest("ops@company.com", "account.unknown")), HttpStatusCode.NotFound, MailErrors.SampleNotFound.Code);
        await AssertCodeAsync(await admin.PostAsJsonAsync(Tests, new MailTestRequest("ops@company.com", "account.reauthentication-code",
            new Dictionary<string, string?> { ["Minutes"] = "十" })), HttpStatusCode.BadRequest, MailErrors.SampleInvalid.Code);
        await AssertCodeAsync(await admin.PostAsJsonAsync(Tests, new MailTestRequest("ops@company.com", "account.security-notice",
            new Dictionary<string, string?> { ["Secret"] = "x" })), HttpStatusCode.BadRequest, MailErrors.SampleInvalid.Code);
        using var scope = factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<OutboundMessage>().AnyAsync());
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
