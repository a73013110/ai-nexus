using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AiNexus.Data.App.AccessControl;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Office.Attendance;
using AiNexus.Features.Office.Attendance.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;
using static AiNexus.IntegrationTests.Support.RecordsSession;

namespace AiNexus.IntegrationTests.Attendance;

/// <summary>出勤紀錄頁的規則說明取自目前的設定，與出勤紀錄同一個授權。</summary>
public sealed class GetAttendanceRulesTests
{
    private const string Rules = "/api/v1/attendance-rules";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    [Fact]
    public async Task TheRulesShowTheConfiguredTimesAndNeedTheRecordsPage()
    {
        await using var session = Create(services: services => services.PostConfigure<AttendanceOptions>(options =>
        {
            options.Workday.FullDay.FlexEnd = new(9, 30);
            options.Workday.FullDay.WorkHours = 9;
        }));
        using var client = await session.Factory.SignedInAsync("bob");

        var rules = (await client.GetFromJsonAsync<AttendanceRulesDto>(Rules, Json))!;

        var full = Assert.Single(rules.Schedules, x => x.DayType == AttendanceDayType.FullDay);
        Assert.Equal((new TimeOnly(9, 30), 9d), (full.FlexEnd, full.WorkHours));
        Assert.Contains("09:40 到，遲到 10 分", Assert.Single(rules.Rules, x => x.Title == "遲到").Text);

        await session.WithDbAsync(db => db.Set<RoleGroupFeature>()
            .Where(x => x.GroupId == BuiltInAccess.WorkspaceGroup && x.FeatureId == FeatureCatalog.AttendanceRecords).ExecuteDeleteAsync());
        await AssertCodeAsync(await client.GetAsync(Rules), HttpStatusCode.Forbidden, "feature_forbidden");
    }
}
