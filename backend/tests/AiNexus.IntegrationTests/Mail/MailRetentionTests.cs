using AiNexus.Data.App;
using AiNexus.Data.App.Mail;
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
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<OutboundMessage>().Where(x => x.Id == pending)
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
        Assert.Equal([pending], await db.Set<OutboundMessage>().Select(x => x.Id).ToListAsync());
        Assert.False(await db.Set<DeliveryAttempt>().AnyAsync(x => x.MessageId == old));
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
