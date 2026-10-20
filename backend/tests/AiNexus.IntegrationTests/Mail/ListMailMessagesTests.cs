using System.Net;
using System.Net.Http.Json;
using AiNexus.Data.App.Mail;
using AiNexus.Features.Admin.Mail;
using AiNexus.Features.Admin.Mail.Messages;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Time.Testing;
using static AiNexus.IntegrationTests.Support.MailFixtures;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.Mail;

public sealed class ListMailMessagesTests
{
    private const string Messages = "/api/v1/admin/mail-messages";

    [Fact]
    public async Task AdministratorsFindMessagesByRecipientMessageIdOrSubjectWithoutSeeingAddresses()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = new AppFactory(administrators: ["alice"], clock: clock);
        using var admin = await factory.SignedInAsync();
        var first = await EnqueueAsync(factory, new Greeting(), "第一封", "chen@company.com");
        clock.Advance(TimeSpan.FromSeconds(1));
        var second = await EnqueueAsync(factory, new Greeting(), "第二封", "wang@company.com");
        clock.Advance(TimeSpan.FromSeconds(1));
        var third = await EnqueueAsync(factory, new Code(), "123456", "chen@company.com");

        var all = (await admin.GetFromJsonAsync<MailMessageDto[]>(Messages))!;
        Assert.Equal([third, second, first], all.Select(x => x.Id));
        Assert.All(all, x => Assert.DoesNotContain("chen@", x.Recipient, StringComparison.Ordinal));
        Assert.Equal("c***n@company.com", all[2].Recipient);

        Assert.Equal([third, first], (await admin.GetFromJsonAsync<MailMessageDto[]>($"{Messages}?search=CHEN@company.com"))!.Select(x => x.Id));
        Assert.Equal([second], (await admin.GetFromJsonAsync<MailMessageDto[]>($"{Messages}?search=%3C{second:N}%40fixture.test%3E"))!.Select(x => x.Id));
        Assert.Equal([first], (await admin.GetFromJsonAsync<MailMessageDto[]>($"{Messages}?search={first}"))!.Select(x => x.Id));
        Assert.Equal([first], (await admin.GetFromJsonAsync<MailMessageDto[]>($"{Messages}?search=你好 第一"))!.Select(x => x.Id));
        Assert.Equal([third], (await admin.GetFromJsonAsync<MailMessageDto[]>($"{Messages}?category=test.code"))!.Select(x => x.Id));

        // 游標分頁：上一頁最後一筆之後。
        Assert.Equal([second, first], (await admin.GetFromJsonAsync<MailMessageDto[]>($"{Messages}?before={third}&take=25"))!.Select(x => x.Id));
        await AssertCodeAsync(await admin.GetAsync($"{Messages}?before={Guid.NewGuid()}"), HttpStatusCode.BadRequest, MailErrors.CursorInvalid.Code);
        await AssertCodeAsync(await admin.GetAsync($"{Messages}?status=lost"), HttpStatusCode.BadRequest, MailErrors.FilterInvalid.Code);
        await AssertCodeAsync(await admin.GetAsync($"{Messages}?take=7"), HttpStatusCode.BadRequest, MailErrors.FilterInvalid.Code);

        Assert.Equal(3, await Dispatcher(factory).DispatchAsync(CancellationToken.None));
        Assert.Equal(3, (await admin.GetFromJsonAsync<MailMessageDto[]>($"{Messages}?status=sent"))!.Length);
        var status = (await admin.GetFromJsonAsync<MailStatusDto>("/api/v1/admin/mail-status"))!;
        Assert.Equal((MailTransports.Smtp, true, "smtp.fixture.test:587（StartTls）", 3, 0), (status.Transport, status.Enabled, status.Server, status.SentLastDay, status.Queued));
    }

    [Fact]
    public async Task DetailsShowEveryAttemptAndHideSensitiveContent()
    {
        await using var factory = new AppFactory(administrators: ["alice"]);
        using var admin = await factory.SignedInAsync();
        var greeting = await EnqueueAsync(factory, new Greeting(), "小陳", "chen@company.com");
        var code = await EnqueueAsync(factory, new Code(), "123456", "chen@company.com");
        factory.Mail.Failure = mail => mail.Id == greeting ? new SmtpCommandException(SmtpErrorCode.RecipientNotAccepted, SmtpStatusCode.MailboxUnavailable, "5.1.1 chen@company.com unknown") : null;
        await Dispatcher(factory).DispatchAsync(CancellationToken.None);

        var failed = (await admin.GetFromJsonAsync<MailMessageDetailDto>($"{Messages}/{greeting}"))!;
        Assert.Equal((MailStates.Failed, $"<{greeting:N}@fixture.test>", true), (failed.Message.Status, failed.HeaderMessageId, failed.RecipientAvailable));
        var attempt = Assert.Single(failed.Attempts);
        Assert.Equal((MailAttemptOutcomes.Permanent, 550, "5.1.1"), (attempt.Outcome, attempt.SmtpStatus, attempt.EnhancedStatus));
        Assert.Contains("這是一封測試信。", failed.Text, StringComparison.Ordinal);
        Assert.True(failed.CanResend);

        var sensitive = (await admin.GetFromJsonAsync<MailMessageDetailDto>($"{Messages}/{code}"))!;
        Assert.Equal((MailStates.Sent, (string?)null, false, MailErrors.ContentUnavailable.Code), (sensitive.Message.Status, sensitive.Text, sensitive.CanResend, sensitive.ResendBlockedBy));
        Assert.NotNull(sensitive.ProviderResponse);
        await AssertCodeAsync(await admin.GetAsync($"{Messages}/{Guid.NewGuid()}"), HttpStatusCode.NotFound, MailErrors.MessageNotFound.Code);
    }

    [Fact]
    public async Task MembersWithoutTheMailPageAreForbidden()
    {
        await using var factory = new AppFactory(administrators: ["alice"]);
        using var member = await factory.SignedInAsync("bob");
        await AssertCodeAsync(await member.GetAsync(Messages), HttpStatusCode.Forbidden, "feature_forbidden");
        await AssertCodeAsync(await member.GetAsync("/api/v1/admin/mail-status"), HttpStatusCode.Forbidden, "feature_forbidden");
        await AssertCodeAsync(await member.PostAsJsonAsync("/api/v1/admin/mail-tests", new MailTestRequest("bob@company.com")), HttpStatusCode.Forbidden, "feature_forbidden");
    }
}
