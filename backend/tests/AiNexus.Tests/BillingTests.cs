using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Account;
using AiNexus.Features.Chat;
using AiNexus.Features.Persistence;
using AiNexus.Features.Inference;
using AiNexus.Features.Billing;
using AiNexus.Features.Dashboard;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AiNexus.Tests;

public sealed class BillingTests
{
    [Fact]
    public void TariffsCountCacheAndReasoningExactlyOnce()
    {
        var call = new ModelCharge { PriceId = Guid.NewGuid(), StartedAt = DateTimeOffset.UtcNow, InputTokens = 1_000_000,
            CachedInputTokens = 400_000, OutputTokens = 100_000, ReasoningTokens = 25_000,
            InputPerMillion = 1, CachedInputPerMillion = .2m, OutputPerMillion = 4, PerRequest = .5m, UsageComplete = true };
        ChargeCalculator.Finalize(call, "completed"); Assert.Equal(1.58m, call.Amount); Assert.Equal("metered", call.State);
        ChargeCalculator.Finalize(call, "completed"); Assert.Equal(1.58m, call.Amount);
    }
    [Theory]
    [InlineData(false, false, "completed", "not_started", 0d)]
    [InlineData(true, false, "completed", "unpriced", null)]
    [InlineData(true, true, "completed", "missing_usage", null)]
    [InlineData(true, true, "running", "pending", null)]
    public void UnknownUsageIsNeverInventedAsZero(bool started, bool priced, string outcome, string state, double? amount)
    {
        var call = new ModelCharge { StartedAt = started ? DateTimeOffset.UtcNow : null, PriceId = priced ? Guid.NewGuid() : null, InputPerMillion = 1, CachedInputPerMillion = 1 };
        ChargeCalculator.Finalize(call, outcome); Assert.Equal(state, call.State); Assert.Equal(amount is null ? null : (decimal?)amount.Value, call.Amount);
    }
    [Fact]
    public void FlatAndFreePricesWorkWithoutTokenMetadata()
    {
        var call = new ModelCharge { StartedAt = DateTimeOffset.UtcNow, PriceId = Guid.NewGuid(), PerRequest = 3, RequestCharge = "attempted" };
        ChargeCalculator.Finalize(call, "failed"); Assert.Equal(3m, call.Amount);
        call.RequestCharge = "completed"; ChargeCalculator.Finalize(call, "failed"); Assert.Equal(0m, call.Amount);
        call.PerRequest = 0; call.Kind = "free"; ChargeCalculator.Finalize(call, "completed"); Assert.Equal(0m, call.Amount); Assert.Equal("metered", call.State);
    }
    [Fact]
    public void PartialStreamCountsAreUnknownUntilFinalUsageArrives()
    {
        var call = new ModelCharge { PriceId = Guid.NewGuid(), StartedAt = DateTimeOffset.UtcNow, InputTokens = 100, OutputTokens = 20, InputPerMillion = 1, CachedInputPerMillion = 1 };
        ChargeCalculator.Finalize(call, "failed"); Assert.Equal("missing_usage", call.State); Assert.Null(call.Amount);
        call.UsageComplete = true; ChargeCalculator.Finalize(call, "failed"); Assert.Equal(.0001m, call.Amount);
    }
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
        var conversation = await ChatApiTests.CreateConversation(bob, "費用測試");
        var request = new CreateRunRequest(conversation.Id, "test-model", "第一則提問", null, null); var key = Guid.NewGuid().ToString();
        var run = await Submit(bob, request, key); await Wait(bob, run.Id);
        var retry = await Submit(bob, request, key); Assert.Equal(run.Id, retry.Id);
        (await alice.PostAsJsonAsync("/api/v1/admin/billing/prices", first with { PerRequest = 9, EffectiveAt = DateTimeOffset.UtcNow })).EnsureSuccessStatusCode();
        var detail = (await bob.GetFromJsonAsync<ConversationDetailDto>($"/api/v1/conversations/{conversation.Id}"))!;
        Assert.Equal(.100135m, detail.Messages.Single(x => x.Role == "assistant").Charge!.Amount);
        var next = await Submit(bob, request with { Prompt = "第二則提問", ParentMessageId = run.AssistantMessageId }, Guid.NewGuid().ToString()); await Wait(bob, next.Id);
        var spend = (await bob.GetFromJsonAsync<ConversationSpendDto>($"/api/v1/conversations/{conversation.Id}/spend"))!;
        Assert.Equal(2, spend.Requests); Assert.Equal(9.100270m, Assert.Single(spend.Totals).Amount);
        Assert.Equal("測試模型", Assert.Single(spend.Models).Label);
        Assert.Equal(HttpStatusCode.NotFound, (await alice.GetAsync($"/api/v1/conversations/{conversation.Id}/spend")).StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        Assert.Equal(2, await db.Set<ModelCharge>().CountAsync(x => x.ConversationId == conversation.Id));
    }
    [Fact]
    public async Task ReportsKeepCurrenciesOwnersAndExclusiveBoundariesSeparate()
    {
        await using var factory = new NexusFactory(administrators: ["alice"]); using var alice = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        var a = (await alice.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id; var b = (await bob.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var start = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.FromHours(8)); var end = start.AddDays(2);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            foreach (var (owner, currency, amount, at) in new[] { (a, "USD", 1m, start), (b, "TWD", 32m, start.AddDays(1)), (b, "TWD", 100m, end) })
                db.Add(new ModelCharge { Id = Guid.NewGuid(), OwnerId = owner, Currency = currency, Kind = "api", Amount = amount, State = "metered", CreatedAt = at, ModelId = "test-model", Provider = "google", InputTokens = 10, OutputTokens = 5 });
            await db.SaveChangesAsync();
        }
        var parameters = $"from={Uri.EscapeDataString(start.ToString("O"))}&until={Uri.EscapeDataString(end.ToString("O"))}&offsetMinutes=480";
        var personal = (await bob.GetFromJsonAsync<SpendReportDto>("/api/v1/billing/spend?" + parameters))!;
        Assert.Equal(32m, Assert.Single(personal.Totals).Amount); Assert.Single(personal.Daily); Assert.Equal("2026-10-02", personal.Daily[0].Label); Assert.Empty(personal.Users);
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.GetAsync("/api/v1/admin/billing/spend?" + parameters)).StatusCode);
        var report = (await alice.GetFromJsonAsync<SpendReportDto>("/api/v1/admin/billing/spend?" + parameters))!;
        Assert.Equal(2, report.Totals.Count); Assert.Equal(2, report.Users.Count); Assert.Equal(2, report.Requests);
        Assert.All(report.Models, x => Assert.Equal("測試模型", x.Label));
        Assert.Equal("測試模型", Assert.Single(personal.Models).Label);
        Assert.Equal(HttpStatusCode.BadRequest, (await alice.GetAsync("/api/v1/billing/spend?from=2020-01-01&until=2026-10-01")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.GetAsync("/api/v1/dashboard?scope=platform")).StatusCode);
        var dashboard = (await bob.GetFromJsonAsync<DashboardDto>("/api/v1/dashboard?" + parameters))!; Assert.Equal(1, dashboard.Spend.Requests);
        var csv = System.Text.Encoding.UTF8.GetString(SpendReports.Csv(report with { Users = [new(a, "=malicious()", "user", "USD", "api", 1, 1, 0)] }));
        Assert.Contains("\"'=malicious()\"", csv);
    }
    [Fact]
    public async Task AdministrativeExportIncludesEveryUserBeyondTwoHundred()
    {
        await using var factory = new NexusFactory(db =>
        {
            for (var i = 0; i < 250; i++)
            {
                var user = new AiNexus.Features.Identity.NexusUser { Sid = "fixture-cost-" + i, Account = "account-" + i, DisplayName = "user-" + i };
                db.Add(user); db.Add(new ModelCharge { Id = Guid.NewGuid(), OwnerId = user.Id, Currency = "USD", Kind = "api", Amount = 1m, State = "metered", CreatedAt = DateTimeOffset.UtcNow, ModelId = "test-model", Provider = "google" });
            }
            db.SaveChanges();
        }, administrators: ["alice"]);
        using var client = await factory.SignedInAsync();
        var report = (await client.GetFromJsonAsync<SpendReportDto>("/api/v1/admin/billing/spend"))!;
        Assert.Equal(250, report.Users.Count); Assert.Equal(250m, Assert.Single(report.Totals).Amount);
        var csv = await client.GetStringAsync("/api/v1/admin/billing/export"); Assert.Equal(251, csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public async Task HiddenModelNamesAreMergedWithoutLeakingRealIds()
    {
        await using var factory = new NexusFactory(inference: o => o.ShowModelNames = false);
        using var client = await factory.SignedInAsync(); var conversation = await ChatApiTests.CreateConversation(client);
        var owner = (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            foreach (var model in new[] { "retired-private-model-A", "retired-private-model-B" })
                db.Add(new ModelCharge { Id = Guid.NewGuid(), OwnerId = owner, ConversationId = conversation.Id, Provider = "google", ModelId = model, CreatedAt = DateTimeOffset.UtcNow, Currency = "USD", Kind = "api", Amount = 1m, State = "metered" });
            await db.SaveChangesAsync();
        }
        var body = await client.GetStringAsync($"/api/v1/conversations/{conversation.Id}/spend"); Assert.DoesNotContain("retired-private", body);
        var spend = System.Text.Json.JsonSerializer.Deserialize<ConversationSpendDto>(body, System.Text.Json.JsonSerializerOptions.Web)!;
        Assert.Equal(2, Assert.Single(spend.Models).Requests); Assert.Equal(2m, spend.Models[0].Amount);
    }

    internal static async Task<RunDto> Submit(HttpClient client, CreateRunRequest body, string key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/runs") { Content = JsonContent.Create(body) }; request.Headers.Add("Idempotency-Key", key);
        var response = await client.SendAsync(request); response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<RunDto>())!;
    }
    internal static async Task Wait(HttpClient client, Guid id)
    {
        Assert.Equal(RunStates.Completed, (await ChatApiTests.WaitForTerminal(client, id)).Status);
    }
}
