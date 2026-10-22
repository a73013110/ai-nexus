using System.Net;
using System.Net.Http.Json;
using AiNexus.Data.App.Attendance;
using AiNexus.Features.Staff.Attendance.Cards;
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
        Assert.Equal(("ab0012", CardKinds.Employee, "備用卡"), (card.CardNumber, card.Kind, card.Note));

        await AssertCodeAsync(await hr.PostAsJsonAsync(Cards, new CardRequest("AB0012", CardKinds.Employee, null)), HttpStatusCode.Conflict, "attendance_card_number_duplicate");
        var other = await CreateCardAsync(hr, "ab0013");
        await AssertCodeAsync(await hr.PutAsJsonAsync($"{Cards}/{other.Id}", new CardRequest("Ab0012 ", CardKinds.Employee, null)), HttpStatusCode.Conflict, "attendance_card_number_duplicate");
        // 改成自己的卡號（只改備註）不算重複。
        (await hr.PutAsJsonAsync($"{Cards}/{other.Id}", new CardRequest("ab0013", CardKinds.Visitor, "訪客用"))).EnsureSuccessStatusCode();

        var cards = (await hr.GetFromJsonAsync<CardDto[]>(Cards))!;
        Assert.Equal(["ab0012", "ab0013"], cards.Select(x => x.CardNumber));
        Assert.Equal([CardKinds.Employee, CardKinds.Visitor], cards.Select(x => x.Kind));
        Assert.Equal(["ab0013"], (await hr.GetFromJsonAsync<CardDto[]>($"{Cards}?kind=visitor"))!.Select(x => x.CardNumber));
        await AssertCodeAsync(await hr.GetAsync($"{Cards}?kind=guest"), HttpStatusCode.BadRequest, "attendance_filter_invalid");
    }

    [Fact]
    public async Task InvalidCardsAreRejectedBeforeTouchingTheDatabase()
    {
        await using var factory = new AppFactory();
        await GrantCardsAsync(factory, await UserIdAsync(factory, "hr"));
        using var hr = await factory.SignedInAsync("hr");

        Assert.Equal(HttpStatusCode.BadRequest, (await hr.PostAsJsonAsync(Cards, new CardRequest("  ", CardKinds.Employee, null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await hr.PostAsJsonAsync(Cards, new CardRequest(new string('9', 33), CardKinds.Employee, null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await hr.PostAsJsonAsync(Cards, new CardRequest("001", "guest", null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await hr.PostAsJsonAsync(Cards, new CardRequest("001", CardKinds.Employee, new string('備', 201)))).StatusCode);
        Assert.Empty((await hr.GetFromJsonAsync<CardDto[]>(Cards))!);
        await AssertCodeAsync(await hr.PutAsJsonAsync($"{Cards}/{Guid.NewGuid()}", new CardRequest("001", CardKinds.Employee, null)), HttpStatusCode.NotFound, "attendance_card_not_found");
        await AssertCodeAsync(await hr.DeleteAsync($"{Cards}/{Guid.NewGuid()}"), HttpStatusCode.NotFound, "attendance_card_not_found");
    }

    [Fact]
    public async Task CardsWithAssignmentsCannotBeDeletedOrTurnedIntoVisitorCards()
    {
        await using var factory = new AppFactory();
        var bob = await UserIdAsync(factory, "bob");
        await GrantCardsAsync(factory, await UserIdAsync(factory, "hr"));
        using var hr = await factory.SignedInAsync("hr");
        var held = await CreateCardAsync(hr, "A100");
        var unused = await CreateCardAsync(hr, "A101");
        await AssignOkAsync(hr, held.Id, bob, At(10, 1));

        await AssertCodeAsync(await hr.DeleteAsync($"{Cards}/{held.Id}"), HttpStatusCode.Conflict, "attendance_card_has_assignments");
        await AssertCodeAsync(await hr.PutAsJsonAsync($"{Cards}/{held.Id}", new CardRequest("A100", CardKinds.Visitor, null)), HttpStatusCode.Conflict, "attendance_card_has_assignments");

        Assert.Equal(HttpStatusCode.NoContent, (await hr.DeleteAsync($"{Cards}/{unused.Id}")).StatusCode);
        var remaining = Assert.Single((await hr.GetFromJsonAsync<CardDto[]>(Cards))!);
        Assert.Equal(("A100", bob), (remaining.CardNumber, remaining.Current?.UserId));
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
    }

    [Fact]
    public async Task CardChangesAreAudited()
    {
        await using var factory = new AppFactory();
        await GrantCardsAsync(factory, await UserIdAsync(factory, "hr"));
        using var hr = await factory.SignedInAsync("hr");
        var card = await CreateCardAsync(hr, "D1");
        (await hr.PutAsJsonAsync($"{Cards}/{card.Id}", new CardRequest("D2", CardKinds.Employee, "改號"))).EnsureSuccessStatusCode();
        await AssertCodeAsync(await hr.PostAsJsonAsync(Cards, new CardRequest("D2", CardKinds.Employee, null)), HttpStatusCode.Conflict, "attendance_card_number_duplicate");
        (await hr.DeleteAsync($"{Cards}/{card.Id}")).EnsureSuccessStatusCode();

        var saved = await AuditAsync(factory, "admin.attendance_card");
        Assert.Equal(["saved", "saved", "attendance_card_number_duplicate"], saved.Select(x => x.Result));
        // 修改的快照有前後值，看得出卡號從哪裡改到哪裡。
        Assert.Contains("D1", saved[1].Details, StringComparison.Ordinal);
        Assert.Contains("D2", saved[1].Details);
        // 備註是自由文字，不進稽核。
        Assert.DoesNotContain("改號", saved[1].Details, StringComparison.Ordinal);
        Assert.Equal(["saved"], (await AuditAsync(factory, "admin.attendance_card_delete")).Select(x => x.Result));
    }
}
