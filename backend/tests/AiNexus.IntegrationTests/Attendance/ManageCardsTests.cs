using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Office.Attendance.Cards;
using static AiNexus.IntegrationTests.Support.AttendanceApi;
using static AiNexus.IntegrationTests.Support.OrganizationApi;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.Attendance;

public sealed class ManageCardsTests
{
    [Fact]
    public async Task CardNumbersAreUniqueIgnoringCaseAndSurroundingSpaces()
    {
        await using var factory = new AppFactory();
        await GrantCardsAsync(factory, await UserIdAsync(factory, "hr"));
        using var hr = await factory.SignedInAsync("hr");

        var card = await CreateCardAsync(hr, " ab0012 ", note: "  備用卡  ");
        Assert.Equal(("ab0012", false, "備用卡"), (card.CardNumber, card.HiddenFromRecords, card.Note));

        await AssertCodeAsync(await hr.PostAsJsonAsync(Cards, new CardCreateRequest("AB0012", false, null)), HttpStatusCode.Conflict, "attendance_card_number_duplicate");
        await CreateCardAsync(hr, "ab0013", hidden: true);

        var cards = (await hr.GetFromJsonAsync<CardDto[]>(Cards))!;
        Assert.Equal(["ab0012", "ab0013"], cards.Select(x => x.CardNumber));
        Assert.Equal([false, true], cards.Select(x => x.HiddenFromRecords));
    }

    [Fact]
    public async Task InvalidCardsAreRejectedBeforeTouchingTheDatabase()
    {
        await using var factory = new AppFactory();
        await GrantCardsAsync(factory, await UserIdAsync(factory, "hr"));
        using var hr = await factory.SignedInAsync("hr");

        Assert.Equal(HttpStatusCode.BadRequest, (await hr.PostAsJsonAsync(Cards, new CardCreateRequest("  ", false, null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await hr.PostAsJsonAsync(Cards, new CardCreateRequest(new string('9', 33), false, null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await hr.PostAsJsonAsync(Cards, new CardCreateRequest("001", false, new string('備', 201)))).StatusCode);
        Assert.Empty((await hr.GetFromJsonAsync<CardDto[]>(Cards))!);
        await AssertCodeAsync(await hr.PutAsJsonAsync($"{Cards}/{Guid.NewGuid()}", new CardUpdateRequest(true, null)), HttpStatusCode.NotFound, "attendance_card_not_found");
        await AssertCodeAsync(await hr.DeleteAsync($"{Cards}/{Guid.NewGuid()}"), HttpStatusCode.NotFound, "attendance_card_not_found");
    }

    [Fact]
    public async Task OnlyCardsNeverUsedCanBeDeleted()
    {
        await using var factory = new AppFactory();
        var bob = await UserIdAsync(factory, "bob");
        await GrantCardsAsync(factory, await UserIdAsync(factory, "hr"));
        using var hr = await factory.SignedInAsync("hr");
        var held = await CreateCardAsync(hr, "A100");
        var unused = await CreateCardAsync(hr, "A101");
        var punched = await CreateCardAsync(hr, "A102");
        await AssignOkAsync(hr, held.Id, bob, At(10, 1));
        // 有刷卡的卡刪掉後，下次匯入又會建回來；有持有期間的刪了就失去歸戶依據。
        await AddPunchesAsync(factory, ("A102", At(10, 2, 9)));

        await AssertCodeAsync(await hr.DeleteAsync($"{Cards}/{held.Id}"), HttpStatusCode.Conflict, "attendance_card_in_use");
        await AssertCodeAsync(await hr.DeleteAsync($"{Cards}/{punched.Id}"), HttpStatusCode.Conflict, "attendance_card_in_use");
        Assert.Equal(HttpStatusCode.NoContent, (await hr.DeleteAsync($"{Cards}/{unused.Id}")).StatusCode);
        Assert.Equal(["A100", "A102"], (await hr.GetFromJsonAsync<CardDto[]>(Cards))!.Select(x => x.CardNumber));
    }

    [Fact]
    public async Task HidingKeepsHoldersAndUpdatesTheNote()
    {
        await using var factory = new AppFactory();
        var bob = await UserIdAsync(factory, "bob");
        await GrantCardsAsync(factory, await UserIdAsync(factory, "hr"));
        using var hr = await factory.SignedInAsync("hr");
        var card = await CreateCardAsync(hr, "B1");
        await AssignOkAsync(hr, card.Id, bob, At(10, 1));

        (await hr.PutAsJsonAsync($"{Cards}/{card.Id}", new CardUpdateRequest(true, "  董事  "))).EnsureSuccessStatusCode();

        var saved = Assert.Single((await hr.GetFromJsonAsync<CardDto[]>(Cards))!);
        Assert.Equal((true, "董事", (Guid?)bob), (saved.HiddenFromRecords, saved.Note, saved.Current?.UserId));
        Assert.Single(await HistoryAsync(hr, card.Id));
    }

    [Fact]
    public async Task ListShowsCurrentHolderAndSearchFindsCardsByHolderName()
    {
        await using var factory = new AppFactory(clock: new Microsoft.Extensions.Time.Testing.FakeTimeProvider(At(10, 15)));
        var bob = await UserIdAsync(factory, "bob");
        await GrantCardsAsync(factory, await UserIdAsync(factory, "hr"));
        using var hr = await factory.SignedInAsync("hr");
        var past = await CreateCardAsync(hr, "C1");
        var current = await CreateCardAsync(hr, "C2");
        var future = await CreateCardAsync(hr, "C3");
        var newcomer = await CreateCardAsync(hr, "C4");
        await AssignOkAsync(hr, past.Id, bob, At(9, 1), At(10, 1));
        await AssignOkAsync(hr, current.Id, bob, At(10, 1));
        await AssignOkAsync(hr, future.Id, bob, At(11, 1));
        await AssignOkAsync(hr, newcomer.Id, null, At(10, 10), holderName: "王新進");

        var cards = (await hr.GetFromJsonAsync<CardDto[]>(Cards))!;
        // 只有涵蓋「現在」的那一段算持有中：結束的與還沒開始的都不算。
        Assert.Equal([null, bob, null, null], cards.Select(x => x.Current?.UserId));
        Assert.Equal("王新進", cards[3].Current?.HolderName);
        Assert.Equal(["C1", "C2", "C3"], (await hr.GetFromJsonAsync<CardDto[]>($"{Cards}?search=bob"))!.Select(x => x.CardNumber));
        Assert.Equal(["C4"], (await hr.GetFromJsonAsync<CardDto[]>($"{Cards}?search={Uri.EscapeDataString("新進")}"))!.Select(x => x.CardNumber));
        Assert.Equal(["C2"], (await hr.GetFromJsonAsync<CardDto[]>($"{Cards}?search=c2"))!.Select(x => x.CardNumber));
        Assert.Equal(["C2", "C4"], (await hr.GetFromJsonAsync<CardDto[]>($"{Cards}?status={CardStatuses.Held}"))!.Select(x => x.CardNumber));
    }

    [Fact]
    public async Task CardChangesAreAudited()
    {
        await using var factory = new AppFactory();
        await GrantCardsAsync(factory, await UserIdAsync(factory, "hr"));
        using var hr = await factory.SignedInAsync("hr");
        var card = await CreateCardAsync(hr, "D1");
        (await hr.PutAsJsonAsync($"{Cards}/{card.Id}", new CardUpdateRequest(true, "外派"))).EnsureSuccessStatusCode();
        await AssertCodeAsync(await hr.PostAsJsonAsync(Cards, new CardCreateRequest("D1", false, null)), HttpStatusCode.Conflict, "attendance_card_number_duplicate");
        (await hr.DeleteAsync($"{Cards}/{card.Id}")).EnsureSuccessStatusCode();

        Assert.Equal(["completed", "attendance_card_number_duplicate"], (await AuditAsync(factory, "attendance.card.created")).Select(x => x.Result));
        var updated = Assert.Single(await AuditAsync(factory, "attendance.card.updated"));
        Assert.Equal("completed", updated.Result);
        // 修改的快照有前後的隱藏旗標。
        Assert.Contains("\"hiddenFromRecords\":false", updated.Details, StringComparison.Ordinal);
        Assert.Contains("\"hiddenFromRecords\":true", updated.Details, StringComparison.Ordinal);
        // 備註是自由文字，不進稽核。
        Assert.DoesNotContain("外派", updated.Details, StringComparison.Ordinal);
        Assert.Equal(["completed"], (await AuditAsync(factory, "attendance.card.deleted")).Select(x => x.Result));
    }
}
