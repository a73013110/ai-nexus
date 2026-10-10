using System.Net.Http.Json;
using AiNexus.Features.Chat;
using AiNexus.Features.Conversations;
using AiNexus.Features.Identity.Users;
using AiNexus.Features.Inference;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.Chat;

public sealed class RunRecoveryTests
{
    [Fact]
    public async Task RestartMarksInterruptedWorkFailedWithoutReissuingInference()
    {
        var interruptedId = Guid.NewGuid();
        await using var factory = new NexusFactory(db =>
        {
            var owner = new NexusUser { Sid = "S-1-5-21-test-alice", Account = "TEST\\alice", DisplayName = "alice", LastSeenAt = DateTimeOffset.UtcNow };
            var conversation = new Conversation { OwnerId = owner.Id };
            var user = new Message { ConversationId = conversation.Id, Content = "原始訊息" };
            var answer = new Message { ConversationId = conversation.Id, ParentId = user.Id, Role = "assistant", Status = RunStates.Running, Content = "既有部分回答", RunId = interruptedId };
            conversation.ActiveLeafId = answer.Id;
            var run = new GenerationRun { Id = interruptedId, OwnerId = owner.Id, ActiveOwnerId = owner.Id, ConversationId = conversation.Id, UserMessageId = user.Id, AssistantMessageId = answer.Id, ModelId = "test-model", Status = RunStates.Running, StartedAt = DateTimeOffset.UtcNow, ReservedTokens = 500, Content = answer.Content, IdempotencyKey = Guid.NewGuid().ToString(), RequestHash = new string('A', 64) };
            db.AddRange(owner, conversation, user, answer, run); db.SaveChanges();
        });
        using var client = await factory.SignedInAsync();
        var run = (await client.GetFromJsonAsync<RunDto>($"/api/v1/runs/{interruptedId}"))!;
        Assert.Equal(RunStates.Failed, run.Status); Assert.Equal("executor_lost", run.ErrorCode); Assert.Equal("既有部分回答", run.Content); Assert.Equal(0, factory.Provider.Calls); Assert.NotNull(run.Timing);
        var budget = Assert.Single((await client.GetFromJsonAsync<AiNexus.Features.Inference.EffectiveModelPolicyDto>("/api/v1/settings/model-policy"))!.Models);
        Assert.Equal(500, budget.ReservedTokens);
    }

    [Fact]
    public async Task RecoveryPreservesAnotherServersLiveRunAndOnlyClaimsExpiredLease()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync();
        var conversation = await ChatApi.CreateConversation(client);
        var created = await ChatApi.CreateRun(client, conversation.Id, "lease fixture");
        await ChatApi.WaitForTerminal(client, created.Id);
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
