using System.Globalization;
using System.Net.Http.Json;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Office.Attendance.Cards;
using static AiNexus.IntegrationTests.Support.AttendanceApi;
using static AiNexus.IntegrationTests.Support.IdentityApi;

namespace AiNexus.IntegrationTests.Attendance;

public sealed class CardAccountReviewTests
{
    [Fact]
    public async Task DisabledHoldersAndActiveFormerHoldersAreListedUntilTheirCardsAreSorted()
    {
        await using var session = await HrSession.CreateAsync(new AppFactory(administrators: ["alice"]));
        var (f, hr) = (session.Factory, session.Hr);
        using var admin = await f.SignedInAsync();
        // 測試身分不在 AD 夾具裡，讓目錄查不到以免混入「AD 找不到」的待辦。
        f.AdDirectory.Unavailable = true;
        var erin = await OrganizationApi.UserIdAsync(f, "erin");
        var now = DateTimeOffset.UtcNow;
        var punched = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, now.Offset).AddDays(-5);

        await AssignOkAsync(hr, session.Card.Id, session.Bob, now.AddDays(-60));
        await AddPunchesAsync(f, (session.Card.CardNumber, punched.ToOffset(Taipei)));
        var returned = await CreateCardAsync(hr, "B002");
        await AssignOkAsync(hr, returned.Id, session.Carol, now.AddDays(-60), now.AddDays(-30));
        await AssignOkAsync(hr, returned.Id, erin, now.AddDays(-30));
        // 持有中的人（bob、erin）與從沒持有過卡的人（hr、alice）都沒有待辦。
        Assert.Empty(await ReviewCodesAsync(admin, session.Bob));
        Assert.Empty(await ReviewCodesAsync(admin, erin));

        await DisableAsync(f, session.Bob);
        var held = (await ReviewsAsync(admin)).Users.Single(x => x.Id == session.Bob).Items.Single();
        Assert.Equal("attendance.card_held", held.Code);
        Assert.Contains($"卡號 A001（最後刷卡 {punched.ToOffset(Taipei).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}）", held.Detail);
        Assert.Equal(FeatureCatalog.RouteOf(FeatureCatalog.AttendanceCards) + "?search=A001", held.Route);

        var lost = (await ReviewsAsync(admin)).Users.Single(x => x.Id == session.Carol).Items.Single();
        Assert.Equal("attendance.no_card", lost.Code);
        Assert.Contains("上次持有卡號 B002", lost.Detail);
        Assert.Contains("這張卡目前由", lost.Detail);

        // 上次持有的卡已在出勤紀錄隱藏（例如董事、外派），HR 本來就不看，不提示。
        (await hr.PutAsJsonAsync($"{Cards}/{returned.Id}", new CardUpdateRequest(true, null))).EnsureSuccessStatusCode();
        Assert.Empty(await ReviewCodesAsync(admin, session.Carol));
    }
}
