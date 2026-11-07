using AiNexus.Data.App;
using AiNexus.Data.App.Audit;
using AiNexus.Data.App.Mail;
using AiNexus.Features.Admin.Mail;
using AiNexus.Features.Admin.Mail.Delivery;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using static AiNexus.IntegrationTests.Support.MailFixtures;

namespace AiNexus.IntegrationTests.Mail;

public sealed class MailRetentionTests
{
    [Fact]
    public async Task ContentRecipientsAndRecordsArePurgedOnTheirOwnSchedules()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = new AppFactory(clock: clock);
        var old = await EnqueueAsync(factory, new Greeting(), "old", "old@company.com");
        var pending = await EnqueueAsync(factory, new Greeting(), "pending", "pending@company.com");
        Assert.Equal(2, await Dispatcher(factory).DispatchAsync(CancellationToken.None));
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().OutboundMessages.Where(x => x.Id == pending)
                .ExecuteUpdateAsync(p => p.SetProperty(x => x.Status, MailStates.Queued).SetProperty(x => x.SentAt, (DateTimeOffset?)null));
        var worker = ActivatorUtilities.CreateInstance<MailRetentionWorker>(factory.Services);

        clock.Advance(TimeSpan.FromDays(31));
        Assert.Equal(new MailPurge(0, 1, 0, 0), await worker.PurgeAsync(CancellationToken.None));
        Assert.Null(await ContentAsync(factory, old));
        // 尚未結束的信不清理。
        Assert.NotNull(await ContentAsync(factory, pending));

        clock.Advance(TimeSpan.FromDays(60));
        Assert.Equal(new MailPurge(0, 0, 1, 0), await worker.PurgeAsync(CancellationToken.None));
        var purged = await MessageAsync(factory, old);
        Assert.Null(purged.RecipientAddress);
        Assert.Equal("o***d@company.com", purged.RecipientMasked);

        clock.Advance(TimeSpan.FromDays(275));
        Assert.Equal(new MailPurge(0, 0, 0, 1), await worker.PurgeAsync(CancellationToken.None));
        using var check = factory.Services.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal([pending], await db.OutboundMessages.Select(x => x.Id).ToListAsync());
        Assert.False(await db.DeliveryAttempts.AnyAsync(x => x.MessageId == old));
        // 每一輪清到資料就記一筆系統稽核，只有筆數；沒清到的一輪不記。
        Assert.Equal(MailPurge.None, await worker.PurgeAsync(CancellationToken.None));
        var cleared = await db.AuditEvents.AsNoTracking().Where(x => x.Action == MailAudit.MessageCleared.Name).OrderBy(x => x.Id).ToListAsync();
        Assert.Equal(3, cleared.Count);
        Assert.All(cleared, x => Assert.Equal((Guid.Empty, AuditEvent.Completed), (x.OwnerId, x.Result)));
        Assert.Equal(["contents", "recipients", "records"], cleared.Select(x => x.DetailsJson!).Select(json => json.Contains("\"contents\":1") ? "contents" : json.Contains("\"recipients\":1") ? "recipients" : json.Contains("\"records\":1") ? "records" : json));
        Assert.DoesNotContain(cleared, x => x.DetailsJson!.Contains("@company.com"));
    }

    [Fact]
    public async Task QueuedMessagesPastTheirExpiryFailWhenNothingClaimsThem()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = new AppFactory(clock: clock);
        var id = await EnqueueAsync(factory, new Code(), "123456", "chen@company.com", new(ExpiresAt: clock.GetUtcNow().AddMinutes(15)));
        clock.Advance(TimeSpan.FromMinutes(16));
        Assert.Equal(new MailPurge(1, 0, 0, 0), await ActivatorUtilities.CreateInstance<MailRetentionWorker>(factory.Services).PurgeAsync(CancellationToken.None));
        Assert.Equal((MailStates.Failed, MailFailures.Expired), ((await MessageAsync(factory, id)).Status, (await MessageAsync(factory, id)).ErrorCode));
        Assert.Null(await ContentAsync(factory, id));
    }
}
