using System.Net.Http.Json;
using AiNexus.Features.Conversations;
using AiNexus.Features.Inference.Runs;
using AiNexus.Features.Knowledge;
using AiNexus.Features.Knowledge.Collections;
using AiNexus.Features.Chat.Transcripts;
using AiNexus.Features.Knowledge.Retrieval;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static AiNexus.IntegrationTests.Support.BackgroundJobs;
using static AiNexus.IntegrationTests.Support.ChatApi;
using static AiNexus.IntegrationTests.Support.KnowledgeApi;

namespace AiNexus.IntegrationTests.Knowledge;

public sealed class ConversationKnowledgeTests
{
    [Fact]
    public async Task RetrievedSourceIsSnapshottedWithPageCitationForGeneration()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync();
        var collection = await CreateCollection(client); var document = await AddDocument(client, collection.Resource.Id, "Travel budget approval requires manager signature."); await Process(factory);
        var conversation = await CreateConversation(client);
        (await client.PutAsJsonAsync($"/api/v1/conversations/{conversation.Id}/knowledge", new KnowledgeSelectionDto([collection.Resource.Id]))).EnsureSuccessStatusCode();
        var run = await CreateRun(client, conversation.Id, "travel budget approval"); await WaitForTerminal(client, run.Id);
        Assert.Contains("manager signature", factory.Provider.LastParameters!.SystemPrompt);
        var detail = (await client.GetFromJsonAsync<ConversationDetailDto>($"/api/v1/conversations/{conversation.Id}"))!;
        var source = Assert.Single(detail.Messages.Single(x => x.Role == "assistant").Sources!);
        Assert.Equal(document.Id, source.DocumentId); Assert.Equal(1, source.PageNumber);
    }

    [Fact]
    public async Task RewriteHistoryFollowsTheBranchOldestFirstAndSkipsEmptyAndUnfinishedAnswersUpToTheTurnLimit()
    {
        var rewriter = new CapturingRewriter();
        await using var factory = new NexusFactory(services: services =>
        {
            services.PostConfigure<KnowledgeOptions>(x => x.QueryRewrite.MaxTurns = 2);
            services.RemoveAll<IQueryRewriter>(); services.AddSingleton<IQueryRewriter>(rewriter);
        });
        using var client = await factory.SignedInAsync(); var seed = await SeedAsync(factory, client);
        var conversation = await CreateConversation(client);
        // 路徑為 u0 → a0 → u1 → a1 → u2 → a2（空）→ u3 → a3（仍在執行）；u1 之下的另一條分支不在此路徑上。
        var path = await AddMessagesAsync(factory, conversation.Id, null,
            ("user", "零問", RunStates.Completed), ("assistant", "零答", RunStates.Completed), ("user", "一問", RunStates.Completed), ("assistant", "一答", RunStates.Completed),
            ("user", "二問", RunStates.Completed), ("assistant", "", RunStates.Completed), ("user", "三問", RunStates.Completed), ("assistant", "三答進行中", RunStates.Running));
        await AddMessagesAsync(factory, conversation.Id, path[2], ("assistant", "旁支", RunStates.Completed));

        var result = await ForRunAsync(factory, seed.Actor, new(conversation.Id, "採購", path[^1], null), seed.Collection);

        Assert.True(result.IsSuccess);
        Assert.Equal([("user", "一問"), ("assistant", "一答"), ("user", "二問"), ("user", "三問")], rewriter.History!.Select(x => (x.Role, x.Text)));
        // 重新產生第三個問題時，它會作為查詢，連同其上方的歷史一起讀取。
        Assert.True((await ForRunAsync(factory, seed.Actor, new(conversation.Id, null, null, path[6]), seed.Collection)).IsSuccess);
        Assert.Equal("三問", rewriter.Query);
        Assert.Equal(["零答", "一問", "一答", "二問"], rewriter.History!.Select(x => x.Text));
    }

    [Fact]
    public async Task RewriteHistoryRejectsACycleAParentOutsideTheConversationAndAnotherUsersConversation()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync(); var seed = await SeedAsync(factory, client);
        using var bob = await factory.SignedInAsync("bob");
        var conversation = await CreateConversation(client);
        var other = await CreateConversation(client);
        var cycle = await AddMessagesAsync(factory, conversation.Id, null, ("user", "問", RunStates.Completed), ("assistant", "答", RunStates.Completed));
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<NexusDbContext>().Messages.Where(x => x.Id == cycle[0]).ExecuteUpdateAsync(p => p.SetProperty(x => x.ParentId, cycle[1]));
        var foreign = await AddMessagesAsync(factory, other.Id, null, ("user", "別的對話", RunStates.Completed));
        var stray = await AddMessagesAsync(factory, conversation.Id, foreign[0], ("assistant", "掛錯父訊息", RunStates.Completed));

        Assert.Equal("invalid_history", (await ForRunAsync(factory, seed.Actor, new(conversation.Id, "採購", cycle[1], null), seed.Collection)).Error!.Code);
        Assert.Equal("invalid_history", (await ForRunAsync(factory, seed.Actor, new(conversation.Id, "採購", stray[0], null), seed.Collection)).Error!.Code);
        var bobId = (await bob.GetFromJsonAsync<AiNexus.Features.Account.MeDto>("/api/v1/me"))!.Id;
        Assert.Equal("conversation_not_found", (await ForRunAsync(factory, bobId, new(conversation.Id, "採購", null, null), seed.Collection)).Error!.Code);
    }

    private static async Task<AiNexus.Platform.Errors.Result<IReadOnlyList<KnowledgeHitDto>>> ForRunAsync(NexusFactory factory, Guid actor, ConversationTurn turn, Guid collection)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<KnowledgeRetrieval>().ForRunAsync(actor, turn, CancellationToken.None, [collection]);
    }

    /// <summary>在 <paramref name="parent"/> 之下建立一串訊息（由舊到新），並依相同順序回傳它們的 id。</summary>
    private static async Task<Guid[]> AddMessagesAsync(NexusFactory factory, Guid conversation, Guid? parent, params (string Role, string Content, string Status)[] chain)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        var ids = new List<Guid>();
        foreach (var (role, content, status) in chain)
        {
            var message = new Message { ConversationId = conversation, ParentId = parent, Role = role, Content = content, Status = status };
            db.Messages.Add(message); ids.Add(message.Id); parent = message.Id;
        }
        await db.SaveChangesAsync();
        return ids.ToArray();
    }

    private sealed class CapturingRewriter : IQueryRewriter
    {
        public string? Query { get; private set; }
        public IReadOnlyList<RewriteTurn>? History { get; private set; }
        public Task<string> RewriteAsync(Guid actor, string query, IReadOnlyList<RewriteTurn> history, CancellationToken ct)
        {
            Query = query; History = history;
            return Task.FromResult(query);
        }
    }
}
