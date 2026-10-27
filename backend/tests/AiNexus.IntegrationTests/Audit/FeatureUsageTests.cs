using System.Net;
using System.Net.Http.Json;
using AiNexus.Data.App;
using AiNexus.Data.App.Audit;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Audit;
using AiNexus.Features.Platform.Account;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.Audit;

public sealed class FeatureUsageTests
{
    private static readonly DateTimeOffset Day = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task UsageRollsActionsIntoPagesCountsPeopleOnceAndSplitsDaysByViewerOffset()
    {
        await using var factory = new AppFactory(administrators: ["alice"]);
        using var alice = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        var a = (await alice.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id; var b = (await bob.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            AuditEvent Event(Guid owner, string feature, double hour, string result = AuditEvent.Completed, Guid? actor = null) => new()
            {
                OwnerId = owner, ActorId = actor, FeatureId = feature, Action = "fixture.usage.read", Category = "access", Result = result, At = Day.AddHours(hour),
            };
            db.AuditEvents.AddRange(
                // UTC 15:30 與 16:30 在 +08:00 分屬 10/01 與 10/02。
                Event(a, FeatureCatalog.AttendanceCards, 15.5), Event(a, FeatureCatalog.AttendanceCardsMaintain, 16.5),
                Event(b, FeatureCatalog.AttendanceCards, 16.5), Event(b, FeatureCatalog.AttendanceCards, 17, "feature_forbidden"),
                // 測試身分：算在實際操作的管理者身上。
                Event(b, FeatureCatalog.AttendanceRecords, 3, actor: a),
                // 期間外與系統動作不列入。
                Event(a, FeatureCatalog.AttendanceCards, -48), new AuditEvent { OwnerId = Guid.Empty, FeatureId = FeatureCatalog.AttendanceCards, Action = "fixture.usage.read",
                    Category = "access", Result = AuditEvent.Completed, At = Day.AddHours(5) });
            await db.SaveChangesAsync();
        }
        var period = $"from={Uri.EscapeDataString(Day.AddDays(-1).ToString("O"))}&until={Uri.EscapeDataString(Day.AddDays(2).ToString("O"))}";

        var report = (await alice.GetFromJsonAsync<FeatureUsageReportDto>("/api/v1/admin/audit/usage?" + period))!;
        var cards = Assert.Single(report.Features, x => x.FeatureId == FeatureCatalog.AttendanceCards);
        Assert.Equal((2, 3, 1, Day.AddHours(16.5)), (cards.Users, cards.Events, cards.Failures, cards.LastUsedAt));
        Assert.DoesNotContain(report.Features, x => x.FeatureId == FeatureCatalog.AttendanceCardsMaintain);
        var records = Assert.Single(report.Features, x => x.FeatureId == FeatureCatalog.AttendanceRecords);
        Assert.Equal((1, 1), (records.Users, records.Events));

        var detail = (await alice.GetFromJsonAsync<FeatureUsageDetailDto>($"/api/v1/admin/audit/usage/{FeatureCatalog.AttendanceCards}?offsetMinutes=480&" + period))!;
        Assert.Equal([(new DateOnly(2026, 10, 1), 1, 1), (new DateOnly(2026, 10, 2), 2, 2)], detail.Days.Select(x => (x.Date, x.Users, x.Events)));
        Assert.Equal([(a, 2), (b, 1)], detail.Users.Select(x => (x.UserId, x.Events)));
        Assert.Equal("alice", detail.Users[0].Account);

        Assert.Equal(HttpStatusCode.NotFound, (await alice.GetAsync($"/api/v1/admin/audit/usage/{FeatureCatalog.AttendanceCardsMaintain}?" + period)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await alice.GetAsync($"/api/v1/admin/audit/usage?from={Uri.EscapeDataString(Day.ToString("O"))}&until={Uri.EscapeDataString(Day.AddDays(400).ToString("O"))}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.GetAsync("/api/v1/admin/audit/usage")).StatusCode);
    }
}
