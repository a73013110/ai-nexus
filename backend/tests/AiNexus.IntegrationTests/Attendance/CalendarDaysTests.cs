using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiNexus.Features.Admin.Identity.Sessions;
using AiNexus.Features.Staff.Attendance.Calendar;
using AiNexus.Features.Staff.Attendance.Gd;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.AttendanceApi;
using static AiNexus.IntegrationTests.Support.IdentityApi;
using static AiNexus.IntegrationTests.Support.OrganizationApi;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.Attendance;

public sealed class CalendarDaysTests
{
    private const string AuditAction = "admin.calendar_day";
    private static readonly DateOnly NationalDay = new(2026, 10, 10);

    private static string Day(DateOnly day) => $"{Calendar}/{day:yyyy-MM-dd}";

    [Fact]
    public async Task TheCalendarListsGdDaysInTheRangeInOrder()
    {
        await using var session = await CalendarSession.CreateAsync();
        session.Gd.AddDay(new(2026, 11, 1));
        session.Gd.AddDay(NationalDay, morningOff: true, afternoonOff: true, remark: "國慶日");
        session.Gd.AddDay(new(2026, 10, 1));
        session.Gd.AddDay(new(2026, 9, 30), afternoonOff: true);

        var days = await session.Hr.GetFromJsonAsync<CalendarDayDto[]>($"{Calendar}?from=2026-10-01&to=2026-10-31", TestContext.Current.CancellationToken);

        Assert.Equal([new CalendarDayDto(new(2026, 10, 1), false, false, null), new CalendarDayDto(NationalDay, true, true, "國慶日")], days!);
    }

    [Theory]
    [InlineData("from=2026-10-01")]
    [InlineData("to=2026-10-31")]
    [InlineData("from=2026-10-31&to=2026-10-01")]
    [InlineData("from=2026-01-01&to=2027-01-02")]
    [InlineData("from=1700-01-01&to=1700-01-31")]
    public async Task AnIncompleteReversedOrOverlongRangeIsRefused(string query)
    {
        await using var session = await CalendarSession.CreateAsync();

        await AssertCodeAsync(await session.Hr.GetAsync($"{Calendar}?{query}", TestContext.Current.CancellationToken), HttpStatusCode.BadRequest, "attendance_calendar_range_invalid");
    }

    [Fact]
    public async Task AFullYearIsTheLongestRange()
    {
        await using var session = await CalendarSession.CreateAsync();
        session.Gd.AddDay(new(2028, 12, 31));

        var days = await session.Hr.GetFromJsonAsync<CalendarDayDto[]>($"{Calendar}?from=2028-01-01&to=2028-12-31", TestContext.Current.CancellationToken);

        Assert.Single(days!);
    }

    [Fact]
    public async Task AnUnreachableGdIsAnErrorRatherThanAnEmptyCalendar()
    {
        await using var session = await CalendarSession.CreateAsync(reachable: false);

        await AssertCodeAsync(await session.Hr.GetAsync($"{Calendar}?from=2026-10-01&to=2026-10-31", TestContext.Current.CancellationToken), HttpStatusCode.ServiceUnavailable, "attendance_gd_unavailable");
    }

    [Fact]
    public async Task UpdatingADayWritesGdWithTheOperatorsGdAccountAndAuditsBeforeAndAfter()
    {
        await using var session = await CalendarSession.CreateAsync();
        session.Gd.AddDay(NationalDay, remark: "待定");
        var ct = TestContext.Current.CancellationToken;

        var response = await session.Hr.PutAsJsonAsync(Day(NationalDay), new CalendarDayRequest(true, true, "  國慶日  "), ct);

        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");
        Assert.Equal(new CalendarDayDto(NationalDay, true, true, "國慶日"), await response.Content.ReadFromJsonAsync<CalendarDayDto>(ct));
        Assert.Equal(new GdDay(true, true, "國慶日", CalendarSession.HrGdId), session.Gd.Day(NationalDay));
        var (result, details) = Assert.Single(await AuditAsync(session.Factory, AuditAction));
        Assert.Equal("saved", result);
        using var json = JsonDocument.Parse(details);
        Assert.Equal("2026-10-10", json.RootElement.GetProperty("resourceKey").GetString());
        AssertSnapshot(json.RootElement.GetProperty("before"), false, false, "待定", "sys");
        AssertSnapshot(json.RootElement.GetProperty("after"), true, true, "國慶日", CalendarSession.HrGdId);
    }

    [Fact]
    public async Task ABlankRemarkClearsIt()
    {
        await using var session = await CalendarSession.CreateAsync();
        session.Gd.AddDay(NationalDay, morningOff: true, remark: "颱風假");

        (await session.Hr.PutAsJsonAsync(Day(NationalDay), new CalendarDayRequest(false, false, "   "), TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();

        Assert.Equal(new GdDay(false, false, null, CalendarSession.HrGdId), session.Gd.Day(NationalDay));
    }

    [Fact]
    public async Task ADayMissingFromGdIsNotCreated()
    {
        await using var session = await CalendarSession.CreateAsync();
        session.Gd.AddDay(NationalDay);

        await AssertCodeAsync(await session.Hr.PutAsJsonAsync(Day(new(2026, 10, 11)), new CalendarDayRequest(true, true, null), TestContext.Current.CancellationToken),
            HttpStatusCode.NotFound, "attendance_calendar_day_not_found");

        Assert.Empty((await session.Hr.GetFromJsonAsync<CalendarDayDto[]>($"{Calendar}?from=2026-10-11&to=2026-10-11", TestContext.Current.CancellationToken))!);
        Assert.Equal(["attendance_calendar_day_not_found"], (await AuditAsync(session.Factory, AuditAction)).Select(x => x.Result));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("nobody")]
    [InlineData("twice")]
    public async Task AnOperatorWithoutExactlyOneGdAccountIsRefusedAndNothingIsWritten(string? adAccount)
    {
        await using var session = await CalendarSession.CreateAsync();
        session.Gd.AddDay(NationalDay);
        session.Gd.AddUser("G-T1", "twice");
        session.Gd.AddUser("G-T2", "twice");
        // 排除停用列後仍有兩筆啟用，維持拒絕。
        session.Gd.AddUser("G-T3", "twice", disabled: true);
        await CalendarSession.SetAdAccountAsync(session.Factory, session.HrId, adAccount);

        await AssertCodeAsync(await session.Hr.PutAsJsonAsync(Day(NationalDay), new CalendarDayRequest(true, true, "國慶日"), TestContext.Current.CancellationToken),
            HttpStatusCode.Forbidden, "attendance_gd_account_unmapped");

        Assert.Equal(new GdDay(false, false, null, "sys"), session.Gd.Day(NationalDay));
        Assert.Equal(["attendance_gd_account_unmapped"], (await AuditAsync(session.Factory, AuditAction)).Select(x => x.Result));
    }

    [Fact]
    public async Task AnOperatorWithADisabledOldGdAccountWritesWithTheEnabledOne()
    {
        await using var session = await CalendarSession.CreateAsync();
        session.Gd.AddDay(NationalDay);
        session.Gd.AddUser("G-OLD", CalendarSession.HrAdAccount, disabled: true);

        (await session.Hr.PutAsJsonAsync(Day(NationalDay), new CalendarDayRequest(true, true, "國慶日"), TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();

        Assert.Equal(new GdDay(true, true, "國慶日", CalendarSession.HrGdId), session.Gd.Day(NationalDay));
    }

    [Fact]
    public async Task UsersWithoutTheCalendarPageAreRefusedAndThePageAloneCannotWrite()
    {
        await using var session = await CalendarSession.CreateAsync();
        session.Gd.AddDay(NationalDay);
        var ct = TestContext.Current.CancellationToken;
        using var member = await session.Factory.SignedInAsync("bob");
        await GrantCalendarAsync(session.Factory, await UserIdAsync(session.Factory, "viewer"), maintain: false);
        using var viewer = await session.Factory.SignedInAsync("viewer");

        await AssertCodeAsync(await member.GetAsync($"{Calendar}?from=2026-10-01&to=2026-10-31", ct), HttpStatusCode.Forbidden, "feature_forbidden");
        await AssertCodeAsync(await member.PutAsJsonAsync(Day(NationalDay), new CalendarDayRequest(true, true, null), ct), HttpStatusCode.Forbidden, "feature_forbidden");
        Assert.Single((await viewer.GetFromJsonAsync<CalendarDayDto[]>($"{Calendar}?from=2026-10-01&to=2026-10-31", ct))!);
        await AssertCodeAsync(await viewer.PutAsJsonAsync(Day(NationalDay), new CalendarDayRequest(true, true, null), ct), HttpStatusCode.Forbidden, "feature_forbidden");

        Assert.Equal(new GdDay(false, false, null, "sys"), session.Gd.Day(NationalDay));
    }

    [Fact]
    public async Task ATestIdentityReadsTheCalendarButCannotWriteGd()
    {
        await using var factory = new AppFactory(ldap: true, administrators: ["alice"]);
        using var admin = factory.CreateClient();
        await Login(admin, "alice");
        var hrId = await CreateUser(admin, LocalAccount());
        await GrantCalendarAsync(factory, hrId);
        (await admin.PostAsJsonAsync("/api/v1/auth/test-identity", new TestIdentityRequest(hrId, "重現人資看到的假日設定"), TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();
        await Csrf(admin);

        // 讀取通過授權（GD 未設定所以是 503，不是 403）；寫入在授權就被擋下，碰不到 GD。
        await AssertCodeAsync(await admin.GetAsync($"{Calendar}?from=2026-10-01&to=2026-10-31", TestContext.Current.CancellationToken), HttpStatusCode.ServiceUnavailable, "attendance_gd_unavailable");
        await AssertCodeAsync(await admin.PutAsJsonAsync(Day(NationalDay), new CalendarDayRequest(true, true, null), TestContext.Current.CancellationToken), HttpStatusCode.Forbidden, "test_admin_read_only");
    }

    [Fact]
    public async Task ByDefaultGdIsWritableSoTheWriteSucceeds()
    {
        await using var session = await CalendarSession.CreateAsync();
        session.Gd.AddDay(NationalDay);

        Assert.False(session.Factory.Services.GetRequiredService<GdAccess>().ReadOnly);
        (await session.Hr.PutAsJsonAsync(Day(NationalDay), new CalendarDayRequest(true, false, null), TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();

        Assert.Equal(new GdDay(true, false, null, CalendarSession.HrGdId), session.Gd.Day(NationalDay));
        Assert.Equal(["saved"], (await AuditAsync(session.Factory, AuditAction)).Select(x => x.Result));
    }

    [Fact]
    public async Task AGdSetToReadOnlyStillListsButRefusesTheWrite()
    {
        await using var session = await CalendarSession.CreateAsync(readOnly: true);
        session.Gd.AddDay(NationalDay);
        var ct = TestContext.Current.CancellationToken;

        Assert.Single((await session.Hr.GetFromJsonAsync<CalendarDayDto[]>($"{Calendar}?from=2026-10-01&to=2026-10-31", ct))!);
        await AssertCodeAsync(await session.Hr.PutAsJsonAsync(Day(NationalDay), new CalendarDayRequest(true, true, null), ct), HttpStatusCode.ServiceUnavailable, "attendance_gd_read_only");

        Assert.Equal(new GdDay(false, false, null, "sys"), session.Gd.Day(NationalDay));
        Assert.Equal(["attendance_gd_read_only"], (await AuditAsync(session.Factory, AuditAction)).Select(x => x.Result));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task AMissingOrUnreachableGdRefusesTheWrite(bool configured, bool reachable)
    {
        await using var session = await CalendarSession.CreateAsync(configured: configured, reachable: reachable);
        session.Gd.AddDay(NationalDay);

        await AssertCodeAsync(await session.Hr.PutAsJsonAsync(Day(NationalDay), new CalendarDayRequest(true, true, null), TestContext.Current.CancellationToken),
            HttpStatusCode.ServiceUnavailable, "attendance_gd_unavailable");

        Assert.Equal(new GdDay(false, false, null, "sys"), session.Gd.Day(NationalDay));
        Assert.Equal(["attendance_gd_unavailable"], (await AuditAsync(session.Factory, AuditAction)).Select(x => x.Result));
    }

    [Fact]
    public async Task ARemarkOverTheCharacterLimitOrWithControlCharactersFailsValidation()
    {
        await using var session = await CalendarSession.CreateAsync();
        session.Gd.AddDay(NationalDay);
        var ct = TestContext.Current.CancellationToken;

        await AssertCodeAsync(await session.Hr.PutAsJsonAsync(Day(NationalDay), new CalendarDayRequest(true, true, new string('a', 256)), ct), HttpStatusCode.BadRequest, "validation_failed");
        await AssertCodeAsync(await session.Hr.PutAsJsonAsync(Day(NationalDay), new CalendarDayRequest(true, true, "國慶\n日"), ct), HttpStatusCode.BadRequest, "validation_failed");

        Assert.Equal(new GdDay(false, false, null, "sys"), session.Gd.Day(NationalDay));
    }

    /// <summary>
    /// 假 GD 與 App 同為 Chinese_Taiwan_Stroke_CI_AS（Big5，中文每字 2 位元組）：127 個中文字＝254 位元組存得下，128 個超過 varchar(255)；
    /// Big5 沒有的字（表情符號）會變成問號。兩種都在 GD 回滾，原值不變。
    /// </summary>
    [Fact]
    public async Task ARemarkGdCannotStoreExactlyIsRefusedAndRolledBack()
    {
        await using var session = await CalendarSession.CreateAsync();
        session.Gd.AddDay(NationalDay, remark: "原備註");
        var ct = TestContext.Current.CancellationToken;

        await AssertCodeAsync(await session.Hr.PutAsJsonAsync(Day(NationalDay), new CalendarDayRequest(true, true, new string('假', 128)), ct), HttpStatusCode.BadRequest, "attendance_calendar_remark_invalid");
        await AssertCodeAsync(await session.Hr.PutAsJsonAsync(Day(NationalDay), new CalendarDayRequest(true, true, "國慶日 🎉"), ct), HttpStatusCode.BadRequest, "attendance_calendar_remark_invalid");
        Assert.Equal(new GdDay(false, false, "原備註", "sys"), session.Gd.Day(NationalDay));

        (await session.Hr.PutAsJsonAsync(Day(NationalDay), new CalendarDayRequest(true, true, new string('假', 127)), ct)).EnsureSuccessStatusCode();
        Assert.Equal(new string('假', 127), session.Gd.Day(NationalDay).Remark);
    }

    private static void AssertSnapshot(JsonElement snapshot, bool morningOff, bool afternoonOff, string? remark, string gdUserId)
    {
        Assert.Equal(morningOff, snapshot.GetProperty("morningOff").GetBoolean());
        Assert.Equal(afternoonOff, snapshot.GetProperty("afternoonOff").GetBoolean());
        Assert.Equal(remark, snapshot.GetProperty("calendarRemark").GetString());
        Assert.Equal(gdUserId, snapshot.GetProperty("gdUserId").GetString());
    }
}
