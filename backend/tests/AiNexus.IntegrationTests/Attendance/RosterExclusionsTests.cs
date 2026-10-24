using System.Net;
using System.Net.Http.Json;
using AiNexus.Data.App.Attendance;
using AiNexus.Features.Office.Attendance.Exclusions;
using static AiNexus.IntegrationTests.Support.AttendanceApi;
using static AiNexus.IntegrationTests.Support.OrganizationApi;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.Attendance;

/// <summary>不列入出勤的名單（使用者與卡號）：卡號對應頁讀取，寫入另需 <c>.maintain</c>，寫入會稽核但不記備註。</summary>
public sealed class RosterExclusionsTests
{
    private const string Exclusions = "/api/v1/attendance-roster-exclusions";

    [Fact]
    public async Task HrExcludesUpdatesTheNoteAndRestoresAPerson()
    {
        await using var session = await HrSession.CreateAsync();

        (await session.Hr.PutAsJsonAsync($"{Exclusions}/{session.Bob}", new RosterExclusionRequest(" 董事 "))).EnsureSuccessStatusCode();
        var excluded = Assert.Single(await ListAsync(session.Hr));
        Assert.Equal((session.Bob, "董事"), (excluded.UserId, excluded.Note));

        // 已標過時 PUT 只改備註，不重複也不改設定時間。
        (await session.Hr.PutAsJsonAsync($"{Exclusions}/{session.Bob}", new RosterExclusionRequest(null))).EnsureSuccessStatusCode();
        var updated = Assert.Single(await ListAsync(session.Hr));
        Assert.Equal(((string?)null, excluded.CreatedAt), (updated.Note, updated.CreatedAt));

        (await session.Hr.DeleteAsync($"{Exclusions}/{session.Bob}")).EnsureSuccessStatusCode();
        Assert.Empty(await ListAsync(session.Hr));
        await AssertCodeAsync(await session.Hr.DeleteAsync($"{Exclusions}/{session.Bob}"), HttpStatusCode.NotFound, "attendance_roster_exclusion_not_found");

        var saved = await AuditAsync(session.Factory, "admin.attendance_roster_exclusion");
        Assert.Equal(["saved", "saved"], saved.Select(x => x.Result));
        Assert.DoesNotContain("董事", saved[0].Details);
        Assert.Equal(["saved", "attendance_roster_exclusion_not_found"], (await AuditAsync(session.Factory, "admin.attendance_roster_exclusion_delete")).Select(x => x.Result));
    }

    [Fact]
    public async Task HrExcludesCardsForDataWithoutAnAccount()
    {
        await using var session = await HrSession.CreateAsync();
        // A001 是已登錄的員工卡，只有姓名的持有人；Z9 沒登錄、只出現在刷卡；V1 是訪客卡。
        await AssignOkAsync(session.Hr, session.Card.Id, null, At(10, 1), holderName: "陳小明");
        await AddPunchesAsync(session.Factory, ("Z9", At(10, 2)));
        await CreateCardAsync(session.Hr, "V1", CardKinds.Visitor);

        (await session.Hr.PutAsJsonAsync($"{Exclusions}/cards/%20A001%20", new RosterExclusionRequest("測試卡"))).EnsureSuccessStatusCode();
        (await session.Hr.PutAsJsonAsync($"{Exclusions}/cards/Z9", new RosterExclusionRequest(null))).EnsureSuccessStatusCode();
        (await session.Hr.PutAsJsonAsync($"{Exclusions}/{session.Bob}", new RosterExclusionRequest(null))).EnsureSuccessStatusCode();

        // 有姓名持有人的卡以姓名排序、沒有的卡號排最後。
        var list = await ListAsync(session.Hr);
        Assert.Equal(
            [((Guid?)session.Bob, (string?)null, (string?)null), (null, "A001", "測試卡"), (null, "Z9", null)],
            list.Select(x => (x.UserId, x.CardNumber, x.Note)));
        Assert.Equal(["陳小明", "卡號 Z9"], list.Skip(1).Select(x => x.DisplayName));

        await AssertCodeAsync(await session.Hr.PutAsJsonAsync($"{Exclusions}/cards/V1", new RosterExclusionRequest(null)), HttpStatusCode.Conflict, "attendance_card_visitor");
        await AssertCodeAsync(await session.Hr.PutAsJsonAsync($"{Exclusions}/cards/NOPE", new RosterExclusionRequest(null)), HttpStatusCode.NotFound, "attendance_card_not_found");

        (await session.Hr.DeleteAsync($"{Exclusions}/cards/Z9")).EnsureSuccessStatusCode();
        await AssertCodeAsync(await session.Hr.DeleteAsync($"{Exclusions}/cards/Z9"), HttpStatusCode.NotFound, "attendance_roster_exclusion_not_found");
        Assert.DoesNotContain(await ListAsync(session.Hr), x => x.CardNumber == "Z9");

        var saved = await AuditAsync(session.Factory, "admin.attendance_roster_exclusion");
        Assert.Contains(saved, x => x.Result == "saved" && x.Details.Contains("A001"));
        Assert.DoesNotContain(saved, x => x.Details.Contains("測試卡"));
    }

    [Fact]
    public async Task UnknownUsersAndOverlongNotesAreRefused()
    {
        await using var session = await HrSession.CreateAsync();

        await AssertCodeAsync(await session.Hr.PutAsJsonAsync($"{Exclusions}/{Guid.NewGuid()}", new RosterExclusionRequest(null)), HttpStatusCode.NotFound, "attendance_user_not_found");
        Assert.Equal(HttpStatusCode.BadRequest, (await session.Hr.PutAsJsonAsync($"{Exclusions}/{session.Bob}", new RosterExclusionRequest(new string('註', 201)))).StatusCode);
        Assert.Empty(await ListAsync(session.Hr));
    }

    [Fact]
    public async Task TheCardsPageAloneReadsAndOthersAreRefused()
    {
        await using var session = await HrSession.CreateAsync();
        await GrantCardsAsync(session.Factory, await UserIdAsync(session.Factory, "viewer"), maintain: false);
        using var viewer = await session.Factory.SignedInAsync("viewer");
        using var member = await session.Factory.SignedInAsync("carol");

        Assert.Empty(await ListAsync(viewer));
        await AssertCodeAsync(await viewer.PutAsJsonAsync($"{Exclusions}/{session.Bob}", new RosterExclusionRequest(null)), HttpStatusCode.Forbidden, "feature_forbidden");
        await AssertCodeAsync(await viewer.DeleteAsync($"{Exclusions}/{session.Bob}"), HttpStatusCode.Forbidden, "feature_forbidden");
        await AssertCodeAsync(await viewer.PutAsJsonAsync($"{Exclusions}/cards/A001", new RosterExclusionRequest(null)), HttpStatusCode.Forbidden, "feature_forbidden");
        await AssertCodeAsync(await viewer.DeleteAsync($"{Exclusions}/cards/A001"), HttpStatusCode.Forbidden, "feature_forbidden");
        await AssertCodeAsync(await member.GetAsync(Exclusions), HttpStatusCode.Forbidden, "feature_forbidden");
    }

    private static async Task<RosterExclusionDto[]> ListAsync(HttpClient client) => (await client.GetFromJsonAsync<RosterExclusionDto[]>(Exclusions))!;
}
