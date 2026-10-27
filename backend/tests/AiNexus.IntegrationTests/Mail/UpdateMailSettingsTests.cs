using System.Net;
using System.Net.Http.Json;
using AiNexus.Data.App.Mail;
using AiNexus.Features.Admin.Audit;
using AiNexus.Features.Admin.Mail.Delivery;
using AiNexus.Features.Admin.Mail.Messages;
using AiNexus.Features.Admin.Mail.Transport;
using MailKit.Net.Smtp;
using static AiNexus.IntegrationTests.Support.MailFixtures;

namespace AiNexus.IntegrationTests.Mail;

public sealed class UpdateMailSettingsTests
{
    private const string Settings = "/api/v1/admin/mail-settings";

    [Fact]
    public async Task TurningAutomaticRetryOffEndsTemporaryFailuresAtOnceWithoutARestart()
    {
        await using var factory = new AppFactory(administrators: ["alice"]);
        using var admin = await factory.SignedInAsync();
        Assert.True((await admin.GetFromJsonAsync<MailStatusDto>("/api/v1/admin/mail-status"))!.AutoRetry);

        var response = await admin.PutAsJsonAsync(Settings, new MailSettingsRequest(false));
        response.EnsureSuccessStatusCode();
        Assert.False((await response.Content.ReadFromJsonAsync<MailSettingsDto>())!.AutoRetry);
        Assert.False((await admin.GetFromJsonAsync<MailStatusDto>("/api/v1/admin/mail-status"))!.AutoRetry);

        var id = await EnqueueAsync(factory, new Greeting(), "小陳", "chen@company.com");
        factory.Mail.Failure = _ => new MailSendException(MailAttemptStages.Recipient, null,
            new SmtpCommandException(SmtpErrorCode.RecipientNotAccepted, SmtpStatusCode.MailboxBusy, "4.2.1 Mailbox busy"));
        await Dispatcher(factory).DispatchAsync(CancellationToken.None);
        var message = await MessageAsync(factory, id);
        Assert.Equal((MailStates.Failed, MailFailures.Deferred), (message.Status, message.ErrorCode));
        var attempt = Assert.Single(await AttemptsAsync(factory, id));
        Assert.Equal((MailAttemptOutcomes.Transient, MailAttemptStages.Recipient), (attempt.Outcome, attempt.Stage));

        // 開回來之後，暫時性失敗照常退避重試。
        (await admin.PutAsJsonAsync(Settings, new MailSettingsRequest(true))).EnsureSuccessStatusCode();
        var next = await EnqueueAsync(factory, new Greeting(), "小王", "wang@company.com");
        await Dispatcher(factory).DispatchAsync(CancellationToken.None);
        Assert.Equal(MailStates.Queued, (await MessageAsync(factory, next)).Status);

        var audit = (await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=mail.settings.updated&result=success"))!;
        Assert.Equal(2, audit.Length);
        Assert.Contains("\"autoRetry\":false", audit[^1].DetailsJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MembersCannotChangeMailSettings()
    {
        await using var factory = new AppFactory(administrators: ["alice"]);
        using var member = await factory.SignedInAsync("bob");
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PutAsJsonAsync(Settings, new MailSettingsRequest(false))).StatusCode);
    }
}
