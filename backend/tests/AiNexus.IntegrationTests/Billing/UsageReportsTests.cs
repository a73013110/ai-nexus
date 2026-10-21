using System.Net.Http.Json;
using AiNexus.Data.App;
using AiNexus.Data.App.Inference;
using AiNexus.Features.Admin.Billing.Reports;
using AiNexus.Features.Platform.Account;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.Billing;

/// <summary>用量紀錄的合併與期間在 <c>inference.udf_UsageEntries</c>；報表經 EF 對應的函式查詢。</summary>
public sealed class UsageReportsTests
{
    [Fact]
    public async Task EntriesMergeChatRunsWithModelCallsOverAHalfOpenPeriod()
    {
        await using var factory = new AppFactory();
        using var alice = await factory.SignedInAsync();
        var owner = (await alice.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var conversation = await ChatApi.CreateConversation(alice);
        await ChatApi.WaitForTerminal(alice, (await ChatApi.CreateRun(alice, conversation.Id, "統計")).Id);
        var from = DateTimeOffset.UtcNow.AddDays(-10);
        var until = from.AddDays(5);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        ModelInvocation Call(DateTimeOffset at, long input) => new() { OwnerId = owner, Kind = "ocr", ModelId = "test-model", Status = "completed", InputTokens = input, OutputTokens = 1, CreatedAt = at };
        db.Set<ModelInvocation>().AddRange(Call(from.AddTicks(-1), 1), Call(from, 10), Call(until.AddTicks(-1), 20), Call(until, 100), Call(DateTimeOffset.UtcNow.AddDays(1), 1000));
        await db.SaveChangesAsync();
        var reports = scope.ServiceProvider.GetRequiredService<UsageReports>();

        // 期間含起點、不含終點；聊天執行在期間之外。
        var period = await reports.TokensAsync(owner, new SpendPeriod(from, until, 0), administrator: true, CancellationToken.None);
        Assert.Equal((2, 30L), (period.Daily.Sum(x => x.Requests), period.Daily.Sum(x => x.InputTokens)));

        // 沒有終點：近 30 天起的全部紀錄，聊天執行的 Kind 為 chat。
        var kinds = await reports.KindsAsync(owner, CancellationToken.None);
        Assert.Equal([("chat", 1), ("ocr", 5)], kinds.Select(x => (x.Kind, x.Requests)));
        Assert.Equal(123 + 1 + 10 + 20 + 100 + 1000, kinds.Sum(x => x.InputTokens));
        Assert.Equal(1, await reports.ActiveOwnersSinceAsync(from, CancellationToken.None));
        Assert.Equal(5, (await reports.AllSinceAsync(from, CancellationToken.None)).Requests);
    }
}
