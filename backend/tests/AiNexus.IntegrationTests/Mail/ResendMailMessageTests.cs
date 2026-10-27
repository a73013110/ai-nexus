using System.Net;
using System.Net.Http.Json;
using AiNexus.Data.App.Mail;
using AiNexus.Features.Admin.Audit;
using AiNexus.Features.Admin.Mail;
using AiNexus.Features.Admin.Mail.Messages;
using static AiNexus.IntegrationTests.Support.MailFixtures;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.Mail;

public sealed class ResendMailMessageTests
{
    private const string Messages = "/api/v1/admin/mail-messages";

    [Fact]
    public async Task ResendingCopiesTheRenderedMailIntoANewAuditedMessageOnlyOncePerRequestKey()
    {
        await using var factory = new AppFactory(administrators: ["alice"]);
        using var admin = await factory.SignedInAsync();
        var original = await EnqueueAsync(factory, new Greeting(), "小陳", "chen@company.com");
        await Dispatcher(factory).DispatchAsync(CancellationToken.None);

        var key = Guid.NewGuid();
        var created = await ResendAsync(admin, original, key);
        var again = await ResendAsync(admin, original, key);
        Assert.False(created.Duplicate);
        Assert.Equal((created.MessageId, true), (again.MessageId, again.Duplicate));

        var copy = await MessageAsync(factory, created.MessageId);
        Assert.Equal((original, MailStates.Queued, "chen@company.com", "你好 小陳"), (copy.ResendOfId, copy.Status, copy.RecipientAddress, copy.Subject));
        Assert.NotEqual((await MessageAsync(factory, original)).HeaderMessageId, copy.HeaderMessageId);
        Assert.Equal((await ContentAsync(factory, original))!.Html, (await ContentAsync(factory, created.MessageId))!.Html);
        Assert.Equal(MailStates.Sent, (await MessageAsync(factory, original)).Status);

        var audit = (await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=mail.message.sent&result=success"))!;
        Assert.Equal(2, audit.Length);
        Assert.Contains(created.MessageId.ToString(), audit[^1].DetailsJson, StringComparison.Ordinal);
        Assert.All(audit, x => Assert.DoesNotContain("chen@company.com", x.DetailsJson, StringComparison.Ordinal));
    }

    [Fact]
    public async Task SensitiveOrPurgedMessagesCannotBeResent()
    {
        await using var factory = new AppFactory(administrators: ["alice"]);
        using var admin = await factory.SignedInAsync();
        var code = await EnqueueAsync(factory, new Code(), "123456", "chen@company.com");
        await AssertCodeAsync(await admin.PostAsync($"{Messages}/{code}/resend", null), HttpStatusCode.Conflict, MailErrors.ContentUnavailable.Code);
        await AssertCodeAsync(await admin.PostAsync($"{Messages}/{Guid.NewGuid()}/resend", null), HttpStatusCode.NotFound, MailErrors.MessageNotFound.Code);
        Assert.NotEmpty((await admin.GetFromJsonAsync<AuditDto[]>($"/api/v1/admin/audit?action=mail.message.sent&result={MailErrors.ContentUnavailable.Code}"))!);
    }

    private static async Task<MailEnqueuedDto> ResendAsync(HttpClient client, Guid id, Guid key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Messages}/{id}/resend");
        request.Headers.Add("Idempotency-Key", key.ToString());
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MailEnqueuedDto>())!;
    }
}
