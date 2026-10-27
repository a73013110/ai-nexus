using AiNexus.Data.App;
using AiNexus.Data.App.Billing;
using AiNexus.Data.App.Identity;
using AiNexus.Data.App.Inference;
using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Platform.Account;
using AiNexus.Features.Admin.Billing.Reports;
using AiNexus.Features.Work.Dashboard;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.Billing;

/// <summary>經端點在 LocalDB 實際呼叫 billing.usp_GetSpendReport 與 billing.usp_GetConversationSpend，檢查回傳欄位的對應。</summary>
public sealed class ReportSpendTests
{
    [Fact]
    public async Task ReportBucketsDaysByViewerOffsetAndCountsUnknownPendingAndLegacyCalls()
    {
        await using var factory = new AppFactory(administrators: ["alice"]);
        using var alice = await factory.SignedInAsync();
        var owner = (await alice.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var day = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero); var metered = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            ModelCharge Charge(Guid id, double hour, decimal? amount, string state) => new()
            {
                Id = id, OwnerId = owner, Provider = "google", ModelId = "test-model", Currency = "USD", Kind = "api", CreatedAt = day.AddHours(hour),
                Amount = amount, State = state, InputTokens = amount is null ? null : 10, OutputTokens = amount is null ? null : 5,
            };
            // UTC 15:30 與 16:30 在 +08:00 分屬 10/01 23:30 與 10/02 00:30；金額未知但待結算的不算未知費用。
            db.AddRange(Charge(metered, 15.5, 1m, "metered"), Charge(Guid.NewGuid(), 16.5, null, "failed"), Charge(Guid.NewGuid(), 2, null, "pending"));
            // 已有費用紀錄的呼叫不算舊版；沒有的只計入呼叫次數。
            db.AddRange(new ModelInvocation { Id = metered, OwnerId = owner, Kind = "ocr", ModelId = "test-model", Status = "completed", CreatedAt = day.AddHours(15.5) },
                new ModelInvocation { OwnerId = owner, Kind = "ocr", ModelId = "test-model", Status = "completed", CreatedAt = day.AddHours(3) });
            await db.SaveChangesAsync();
        }
        string Period(int offset) => $"from={Uri.EscapeDataString(day.AddDays(-1).ToString("O"))}&until={Uri.EscapeDataString(day.AddDays(2).ToString("O"))}&offsetMinutes={offset}";

        var taipei = (await alice.GetFromJsonAsync<SpendReportDto>("/api/v1/billing/spend?" + Period(480)))!;
        Assert.Equal((4, 1, 1, 10L, 5L), (taipei.Requests, taipei.PendingCalls, taipei.LegacyCalls, taipei.InputTokens, taipei.OutputTokens));
        Assert.Equal(new MoneyTotalDto("USD", "api", 1m, 1, 1), Assert.Single(taipei.Totals));
        Assert.Equal([("2026-10-01", 2, 0, 1m), ("2026-10-02", 1, 1, 0m)], taipei.Daily.Select(x => (x.Label, x.Requests, x.UnknownCalls, x.Amount)));
        var model = Assert.Single(taipei.Models);
        Assert.Equal(("測試模型", 3, 1, 1m, 10L, 5L), (model.Label, model.Requests, model.UnknownCalls, model.Amount, model.InputTokens, model.OutputTokens));
        Assert.Empty(taipei.Users);
        var utc = (await alice.GetFromJsonAsync<SpendReportDto>("/api/v1/billing/spend?" + Period(0)))!;
        Assert.Equal(("2026-10-01", 3), (Assert.Single(utc.Daily).Label, utc.Daily[0].Requests));
        var platform = (await alice.GetFromJsonAsync<SpendReportDto>($"/api/v1/admin/billing/spend?ownerId={owner}&" + Period(480)))!;
        var user = Assert.Single(platform.Users);
        Assert.Equal((owner, 1m, 3, 1), (user.OwnerId, user.Amount, user.Requests, user.UnknownCalls));
    }

    [Fact]
    public async Task ConversationSpendCountsRunsWithoutChargesAsLegacyAndHidesDeletedConversations()
    {
        await using var factory = new AppFactory();
        using var client = await factory.SignedInAsync();
        var owner = (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var conversation = await ChatApi.CreateConversation(client);
        var run = await ChatApi.CreateRun(client, conversation.Id, "計費上線前的提問"); await ChatApi.WaitForTerminal(client, run.Id);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // 計費上線前的執行紀錄沒有費用紀錄。
            Assert.Equal(1, await db.Set<ModelCharge>().Where(x => x.Id == run.Id).ExecuteDeleteAsync());
            foreach (var (amount, state) in new[] { (2m, "metered"), ((decimal?)null, "failed"), (null, "pending") })
                db.Add(new ModelCharge { Id = Guid.NewGuid(), OwnerId = owner, ConversationId = conversation.Id, Provider = "google", ModelId = "test-model", Currency = "USD", Kind = "api", Amount = amount, State = state, CreatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }
        var spend = (await client.GetFromJsonAsync<ConversationSpendDto>($"/api/v1/conversations/{conversation.Id}/spend"))!;
        Assert.Equal((4, 1, 1), (spend.Requests, spend.PendingCalls, spend.LegacyCalls));
        Assert.Equal(new MoneyTotalDto("USD", "api", 2m, 1, 1), Assert.Single(spend.Totals));
        Assert.Equal(("測試模型", 3, 1), (Assert.Single(spend.Models).Label, spend.Models[0].Requests, spend.Models[0].UnknownCalls));
        (await client.DeleteAsync($"/api/v1/conversations/{conversation.Id}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/conversations/{conversation.Id}/spend")).StatusCode);
    }

    [Fact]
    public async Task ReportsKeepCurrenciesOwnersAndExclusiveBoundariesSeparate()
    {
        await using var factory = new AppFactory(administrators: ["alice"]); using var alice = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        var a = (await alice.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id; var b = (await bob.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var start = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.FromHours(8)); var end = start.AddDays(2);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
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
        await using var factory = new AppFactory(db =>
        {
            for (var i = 0; i < 250; i++)
            {
                var user = new User { Sid = "fixture-cost-" + i, AdAccount = "account-" + i, DisplayName = "user-" + i };
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
        await using var factory = new AppFactory(inference: o => o.ShowModelNames = false);
        using var client = await factory.SignedInAsync(); var conversation = await ChatApi.CreateConversation(client);
        var owner = (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            foreach (var model in new[] { "retired-private-model-A", "retired-private-model-B" })
                db.Add(new ModelCharge { Id = Guid.NewGuid(), OwnerId = owner, ConversationId = conversation.Id, Provider = "google", ModelId = model, CreatedAt = DateTimeOffset.UtcNow, Currency = "USD", Kind = "api", Amount = 1m, State = "metered" });
            await db.SaveChangesAsync();
        }
        var body = await client.GetStringAsync($"/api/v1/conversations/{conversation.Id}/spend"); Assert.DoesNotContain("retired-private", body);
        var spend = System.Text.Json.JsonSerializer.Deserialize<ConversationSpendDto>(body, System.Text.Json.JsonSerializerOptions.Web)!;
        Assert.Equal(2, Assert.Single(spend.Models).Requests); Assert.Equal(2m, spend.Models[0].Amount);
    }
}
