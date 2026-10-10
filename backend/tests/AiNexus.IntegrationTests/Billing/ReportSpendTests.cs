using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Account;
using AiNexus.Features.Billing;
using AiNexus.Features.Dashboard;
using AiNexus.Features.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.Billing;

public sealed class ReportSpendTests
{
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
                var user = new AiNexus.Features.Identity.Users.NexusUser { Sid = "fixture-cost-" + i, Account = "account-" + i, DisplayName = "user-" + i };
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
        using var client = await factory.SignedInAsync(); var conversation = await ChatApi.CreateConversation(client);
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
}
