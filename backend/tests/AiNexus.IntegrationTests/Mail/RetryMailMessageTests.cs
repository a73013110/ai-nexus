using System.Net;
using System.Net.Http.Json;
using AiNexus.Data.App.Mail;
using AiNexus.Features.Admin.Audit;
using AiNexus.Features.Admin.Mail;
using AiNexus.Features.Admin.Mail.Delivery;
using AiNexus.Features.Admin.Mail.Messages;
using MailKit.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using static AiNexus.IntegrationTests.Support.MailFixtures;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.Mail;

public sealed class RetryMailMessageTests
{
    private const string Messages = "/api/v1/admin/mail-messages";

    [Fact]
    public async Task RetryingAWaitingMessageSendsItNowAndEndsThePause()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = new AppFactory(clock: clock, administrators: ["alice"]);
        using var admin = await factory.SignedInAsync();
        var id = await EnqueueAsync(factory, new Greeting(), "小陳", "chen@company.com");
        factory.Mail.Failure = _ => new AuthenticationException("535 5.7.8 Username and Password not accepted");
        await Dispatcher(factory).DispatchAsync(CancellationToken.None);
        var circuit = factory.Services.GetRequiredService<MailCircuit>();
        Assert.True(circuit.IsOpen);

        // 修好設定後立即重試：不必等暫停結束與原本的下次嘗試時間。
        factory.Mail.Failure = null;
        var response = await admin.PostAsync($"{Messages}/{id}/retry", null);
        response.EnsureSuccessStatusCode();
        var retried = (await response.Content.ReadFromJsonAsync<MailMessageDto>())!;
        Assert.Equal((MailStates.Queued, clock.GetUtcNow()), (retried.Status, retried.NextAttemptAt));
        Assert.False(circuit.IsOpen);
        Assert.Equal(1, await Dispatcher(factory).DispatchAsync(CancellationToken.None));
        Assert.Equal((MailStates.Sent, 2), ((await MessageAsync(factory, id)).Status, (await MessageAsync(factory, id)).Attempts));

        var audit = Assert.Single((await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=admin.mail_retry&result=saved"))!);
        Assert.DoesNotContain("chen@company.com", audit.DetailsJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FinishedOrUnknownMessagesCannotBeRetried()
    {
        await using var factory = new AppFactory(administrators: ["alice"]);
        using var admin = await factory.SignedInAsync();
        var id = await EnqueueAsync(factory, new Greeting(), "小陳", "chen@company.com");
        await Dispatcher(factory).DispatchAsync(CancellationToken.None);
        await AssertCodeAsync(await admin.PostAsync($"{Messages}/{id}/retry", null), HttpStatusCode.Conflict, MailErrors.NotRetryable.Code);
        await AssertCodeAsync(await admin.PostAsync($"{Messages}/{Guid.NewGuid()}/retry", null), HttpStatusCode.NotFound, MailErrors.MessageNotFound.Code);
        Assert.Equal(MailStates.Sent, (await MessageAsync(factory, id)).Status);

        using var member = await factory.SignedInAsync("bob");
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PostAsync($"{Messages}/{id}/retry", null)).StatusCode);
    }
}
