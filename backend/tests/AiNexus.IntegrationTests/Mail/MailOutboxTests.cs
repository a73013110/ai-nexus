using AiNexus.Data.App;
using AiNexus.Data.App.Mail;
using AiNexus.Features.Admin.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.MailFixtures;

namespace AiNexus.IntegrationTests.Mail;

public sealed class MailOutboxTests
{
    [Fact]
    public async Task MessagesAreWrittenOnlyWithTheCallersTransaction()
    {
        await using var factory = new AppFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync();
            var enqueued = await scope.ServiceProvider.GetRequiredService<MailOutbox>().EnqueueAsync(new Greeting(), "小陳", new("chen@company.com", "陳小明"), null, CancellationToken.None);
            Assert.True(enqueued.IsSuccess);
            // 入列不自行儲存：業務還沒 SaveChanges 前資料庫沒有這封信。
            Assert.False(await db.Set<OutboundMessage>().AsNoTracking().AnyAsync());
            await db.SaveChangesAsync();
            await transaction.RollbackAsync();
        }
        using (var scope = factory.Services.CreateScope())
            Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<OutboundMessage>().AnyAsync());

        var id = await EnqueueAsync(factory, new Greeting(), "小陳", "Chen@Company.COM");
        var message = await MessageAsync(factory, id);
        Assert.Equal((MailStates.Queued, "test.greeting", MailPriority.Normal, "你好 小陳"), (message.Status, message.Category, message.Priority, message.Subject));
        Assert.Equal(("Chen@company.com", "C***n@company.com"), (message.RecipientAddress, message.RecipientMasked));
        Assert.Equal(MailAddresses.Hash("chen@company.com"), message.RecipientHash);
        Assert.Equal($"<{id:N}@fixture.test>", message.HeaderMessageId);
        Assert.Equal("ainexus@fixture.test", message.FromAddress);
        var content = (await ContentAsync(factory, id))!;
        Assert.False(content.Protected);
        Assert.Contains("這是一封測試信。", content.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidOrBlockedRecipientsAddNothingAndDuplicateKeysReturnTheExistingMessage()
    {
        await using var factory = new AppFactory(services: services => services.PostConfigure<MailOptions>(x => x.AllowedRecipientDomains = ["company.com"]));
        using var scope = factory.Services.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<MailOutbox>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(MailErrors.AddressInvalid, (await outbox.EnqueueAsync(new Greeting(), "x", new("\"Eve\" <eve@company.com>"), null, CancellationToken.None)).Error);
        Assert.Equal(MailErrors.RecipientBlocked, (await outbox.EnqueueAsync(new Greeting(), "x", new("eve@partner.test"), null, CancellationToken.None)).Error);
        Assert.Empty(db.ChangeTracker.Entries());

        var key = new MailEnqueueOptions(IdempotencyKey: "test.greeting:42");
        var first = (await outbox.EnqueueAsync(new Greeting(), "x", new("chen@company.com"), key, CancellationToken.None)).Value!;
        // 同一交易內尚未儲存的列也要擋下，否則會撞唯一索引。
        var local = (await outbox.EnqueueAsync(new Greeting(), "x", new("chen@company.com"), key, CancellationToken.None)).Value!;
        await db.SaveChangesAsync();
        var stored = (await outbox.EnqueueAsync(new Greeting(), "x", new("chen@company.com"), key, CancellationToken.None)).Value!;
        Assert.False(first.Duplicate);
        Assert.Equal((first.MessageId, true), (local.MessageId, local.Duplicate));
        Assert.Equal((first.MessageId, true), (stored.MessageId, stored.Duplicate));
        Assert.Equal(1, await db.Set<OutboundMessage>().CountAsync());
    }

    [Fact]
    public async Task BatchesKeepInvalidRecipientsAsFailedRowsAndSensitiveContentIsEncrypted()
    {
        await using var factory = new AppFactory();
        MailBatch batch;
        using (var scope = factory.Services.CreateScope())
        {
            var outbox = scope.ServiceProvider.GetRequiredService<MailOutbox>();
            batch = await outbox.EnqueueManyAsync(new Code(), [(new MailRecipient("a@company.com"), "111111"), (new MailRecipient("not an address"), "222222"), (new MailRecipient("b@company.com"), "333333")],
                new(IdempotencyKey: "test.code:batch"), CancellationToken.None);
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().SaveChangesAsync();
        }
        Assert.Equal(3, batch.MessageIds.Count);
        Assert.Equal(1, batch.Rejected);
        var rejected = await MessageAsync(factory, batch.MessageIds[1]);
        Assert.Equal((MailStates.Failed, MailErrors.AddressInvalid.Code, (string?)null, batch.BatchId), (rejected.Status, rejected.ErrorCode, rejected.RecipientAddress, rejected.BatchId));
        Assert.Null(await ContentAsync(factory, rejected.Id));

        var sensitive = await MessageAsync(factory, batch.MessageIds[0]);
        Assert.Equal((true, MailPriority.Security, "test.code:batch:0"), (sensitive.Sensitive, sensitive.Priority, sensitive.IdempotencyKey));
        var content = (await ContentAsync(factory, sensitive.Id))!;
        Assert.True(content.Protected);
        Assert.DoesNotContain("111111", content.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("111111", content.Html!, StringComparison.Ordinal);

        // 同一批重送時，已入列的收件者以冪等鍵略過。
        using (var scope = factory.Services.CreateScope())
        {
            var again = await scope.ServiceProvider.GetRequiredService<MailOutbox>().EnqueueManyAsync(new Code(), [(new MailRecipient("a@company.com"), "111111")],
                new(IdempotencyKey: "test.code:batch"), CancellationToken.None);
            Assert.Equal(batch.MessageIds[0], Assert.Single(again.MessageIds));
        }
    }

    [Fact]
    public async Task CancellingAQueuedMessageDeletesItsContentAndDeliveryReportsTheState()
    {
        await using var factory = new AppFactory();
        var id = await EnqueueAsync(factory, new Code(), "123456", "chen@company.com");
        using var scope = factory.Services.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<MailOutbox>();
        Assert.Equal(MailStates.Queued, (await outbox.GetDeliveryAsync(id, CancellationToken.None))!.Status);
        Assert.True(await outbox.CancelAsync(id, CancellationToken.None));
        Assert.False(await outbox.CancelAsync(id, CancellationToken.None));
        Assert.Equal(MailStates.Cancelled, (await outbox.GetDeliveryAsync(id, CancellationToken.None))!.Status);
        Assert.Null(await ContentAsync(factory, id));
        Assert.Null(await outbox.GetDeliveryAsync(Guid.NewGuid(), CancellationToken.None));
    }
}
