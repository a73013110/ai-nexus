using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Billing;
using AiNexus.Features.Chat;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.ChatApi;

namespace AiNexus.IntegrationTests.Billing;

public sealed class ManagePricesTests
{
    [Fact]
    public async Task MalformedPriceFieldsAreRejectedWithoutWritingARecord()
    {
        await using var factory = new NexusFactory(administrators: ["alice"]);
        using var client = await factory.SignedInAsync();
        var valid = new PriceRequest("google", "test-model", "USD", "api", 1, 1, 2, 0, "completed", DateTimeOffset.UtcNow, "fixture");
        foreach (var invalid in new[] { valid with { ModelId = null! }, valid with { Currency = null! }, valid with { Note = null! },
            valid with { CachedInputPerMillion = 2 }, valid with { Kind = "free", PerRequest = 1 } })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/admin/billing/prices", invalid)).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<PriceDto[]>("/api/v1/admin/billing/prices"))!);
    }

    [Fact]
    public async Task PricesAreSnapshottedAndRetriesDoNotDoubleCharge()
    {
        await using var factory = new NexusFactory(administrators: ["alice"]);
        using var alice = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        var first = new PriceRequest("google", "test-model", "USD", "api", 1, 1, 2, .1m, "completed", DateTimeOffset.UtcNow, "fixture");
        (await alice.PostAsJsonAsync("/api/v1/admin/billing/prices", first)).EnsureSuccessStatusCode();
        Assert.Equal("測試模型", Assert.Single((await alice.GetFromJsonAsync<PriceDto[]>("/api/v1/admin/billing/prices"))!).ModelDisplayName);
        var targets = (await alice.GetFromJsonAsync<PriceTargetDto[]>("/api/v1/admin/billing/targets"))!;
        Assert.Contains(targets, x => x.ModelId == "test-model" && x.DisplayName == "測試模型");
        Assert.Contains(targets, x => x.ModelId == "web-search" && x.DisplayName == "網路搜尋");
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.GetAsync("/api/v1/admin/billing/targets")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.GetAsync("/api/v1/admin/billing/prices")).StatusCode);
        var conversation = await ChatApi.CreateConversation(bob, "費用測試");
        var request = new CreateRunRequest(conversation.Id, "test-model", "第一則提問", null, null); var key = Guid.NewGuid().ToString();
        var run = await SubmitRun(bob, request, key); await WaitForCompletion(bob, run.Id);
        var retry = await SubmitRun(bob, request, key); Assert.Equal(run.Id, retry.Id);
        (await alice.PostAsJsonAsync("/api/v1/admin/billing/prices", first with { PerRequest = 9, EffectiveAt = DateTimeOffset.UtcNow })).EnsureSuccessStatusCode();
        var detail = (await bob.GetFromJsonAsync<ConversationDetailDto>($"/api/v1/conversations/{conversation.Id}"))!;
        Assert.Equal(.100135m, detail.Messages.Single(x => x.Role == "assistant").Charge!.Amount);
        var next = await SubmitRun(bob, request with { Prompt = "第二則提問", ParentMessageId = run.AssistantMessageId }, Guid.NewGuid().ToString()); await WaitForCompletion(bob, next.Id);
        var spend = (await bob.GetFromJsonAsync<ConversationSpendDto>($"/api/v1/conversations/{conversation.Id}/spend"))!;
        Assert.Equal(2, spend.Requests); Assert.Equal(9.100270m, Assert.Single(spend.Totals).Amount);
        Assert.Equal("測試模型", Assert.Single(spend.Models).Label);
        Assert.Equal(HttpStatusCode.NotFound, (await alice.GetAsync($"/api/v1/conversations/{conversation.Id}/spend")).StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        Assert.Equal(2, await db.Set<ModelCharge>().CountAsync(x => x.ConversationId == conversation.Id));
    }
}
