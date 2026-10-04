using AiNexus.BuildingBlocks;
using AiNexus.Modules.Inference;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AiNexus.Tests;

public sealed class RunLeaseTests
{
    [Fact]
    public async Task RecoveryPreservesAnotherServersLiveRunAndOnlyClaimsExpiredLease()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync();
        var conversation = await ChatApiTests.CreateConversation(client);
        var created = await ChatApiTests.CreateRun(client, conversation.Id, "lease fixture");
        await ChatApiTests.WaitForTerminal(client, created.Id);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        var run = await db.Runs.SingleAsync(x => x.Id == created.Id);
        run.Status = RunStates.Running; run.ActiveOwnerId = run.OwnerId; run.ExecutorId = Guid.NewGuid();
        run.LeaseExpiresAt = DateTimeOffset.UtcNow.AddMinutes(2); run.Content = "partial answer";
        await db.SaveChangesAsync();
        var recovery = scope.ServiceProvider.GetRequiredService<RunLeaseRecovery>();
        Assert.Equal(0, await recovery.RecoverAsync(DateTimeOffset.UtcNow, default));
        Assert.Equal(RunStates.Running, run.Status);
        run.LeaseExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1); await db.SaveChangesAsync();
        Assert.Equal(1, await recovery.RecoverAsync(DateTimeOffset.UtcNow, default));
        Assert.Equal("executor_lost", run.ErrorCode); Assert.Null(run.ActiveOwnerId);
        Assert.Equal("partial answer", (await db.Messages.SingleAsync(x => x.Id == run.AssistantMessageId)).Content);
        Assert.Equal(0, await recovery.RecoverAsync(DateTimeOffset.UtcNow, default));
        Assert.Single(await db.AuditEvents.Where(x => x.ResourceId == run.Id && x.Action == "run.recovered").ToArrayAsync());
    }
}
