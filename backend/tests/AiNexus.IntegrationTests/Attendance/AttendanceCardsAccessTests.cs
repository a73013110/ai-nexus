using System.Net;
using System.Net.Http.Json;
using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Data.App.Attendance;
using AiNexus.Features.Admin.Identity.Sessions;
using AiNexus.Features.Staff.Attendance.Cards;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.AttendanceApi;
using static AiNexus.IntegrationTests.Support.IdentityApi;
using static AiNexus.IntegrationTests.Support.OrganizationApi;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.Attendance;

public sealed class AttendanceCardsAccessTests
{
    [Fact]
    public async Task UsersWithoutTheCardsPageAreRefusedEverywhere()
    {
        await using var session = await HrSession.CreateAsync();
        using var member = await session.Factory.SignedInAsync("bob");
        var any = Guid.NewGuid();

        foreach (var response in new[]
        {
            await member.GetAsync(Cards),
            await member.GetAsync($"{Assignments}?cardId={session.Card.Id}"),
            await member.GetAsync(Unattributed),
            await member.PostAsJsonAsync(Cards, new CardRequest("X1", CardKinds.Employee, null)),
            await member.PutAsJsonAsync($"{Cards}/{session.Card.Id}", new CardRequest("X1", CardKinds.Employee, null)),
            await member.DeleteAsync($"{Cards}/{session.Card.Id}"),
            await AssignAsync(member, session.Card.Id, session.Bob, At(10, 1)),
            await member.PutAsJsonAsync($"{Assignments}/{any}", new CardAssignmentUpdateRequest(session.Bob, null, At(10, 1), null, null)),
            await member.DeleteAsync($"{Assignments}/{any}"),
        })
            await AssertCodeAsync(response, HttpStatusCode.Forbidden, "feature_forbidden");
        Assert.Single((await session.Hr.GetFromJsonAsync<CardDto[]>(Cards))!);
    }

    [Fact]
    public async Task TheCardsPageAloneReadsButCannotMaintain()
    {
        await using var session = await HrSession.CreateAsync();
        var assignment = await AssignOkAsync(session.Hr, session.Card.Id, session.Bob, At(10, 1));
        await GrantCardsAsync(session.Factory, await UserIdAsync(session.Factory, "viewer"), maintain: false);
        using var viewer = await session.Factory.SignedInAsync("viewer");

        Assert.Single((await viewer.GetFromJsonAsync<CardDto[]>(Cards))!);
        Assert.Single(await HistoryAsync(viewer, session.Card.Id));
        Assert.Empty((await viewer.GetFromJsonAsync<UnattributedCardDto[]>(Unattributed))!);

        foreach (var response in new[]
        {
            await viewer.PostAsJsonAsync(Cards, new CardRequest("X1", CardKinds.Employee, null)),
            await viewer.PutAsJsonAsync($"{Cards}/{session.Card.Id}", new CardRequest("X1", CardKinds.Employee, null)),
            await viewer.DeleteAsync($"{Cards}/{session.Card.Id}"),
            await AssignAsync(viewer, session.Card.Id, session.Carol, At(10, 5), takeOver: true),
            await viewer.PutAsJsonAsync($"{Assignments}/{assignment.Id}", new CardAssignmentUpdateRequest(session.Bob, null, At(10, 1), At(10, 2), null)),
            await viewer.DeleteAsync($"{Assignments}/{assignment.Id}"),
        })
            await AssertCodeAsync(response, HttpStatusCode.Forbidden, "feature_forbidden");
        var unchanged = Assert.Single(await HistoryAsync(session.Hr, session.Card.Id));
        Assert.Equal((assignment.Id, null), (unchanged.Id, unchanged.EndsAt));
    }

    [Fact]
    public async Task RevokingTheGrantTakesEffectOnTheNextRequest()
    {
        await using var session = await HrSession.CreateAsync();
        Assert.Single((await session.Hr.GetFromJsonAsync<CardDto[]>(Cards))!);

        await RevokeAsync(session.Factory, "test-hr");

        await AssertCodeAsync(await session.Hr.GetAsync(Cards), HttpStatusCode.Forbidden, "feature_forbidden");
        await AssertCodeAsync(await session.Hr.PostAsJsonAsync(Cards, new CardRequest("X1", CardKinds.Employee, null)), HttpStatusCode.Forbidden, "feature_forbidden");
    }

    [Fact]
    public async Task ATestIdentityReadsCardsButCannotMaintainThem()
    {
        await using var factory = new AppFactory(ldap: true, administrators: ["alice"]);
        using var admin = factory.CreateClient();
        await Login(admin, "alice");
        var hrId = await CreateUser(admin, LocalAccount());
        await GrantCardsAsync(factory, hrId);
        (await admin.PostAsJsonAsync("/api/v1/auth/test-identity", new TestIdentityRequest(hrId, "重現人資看到的卡號對應"))).EnsureSuccessStatusCode();
        await Csrf(admin);

        // 卡號對應是管理頁：測試身分可以讀取以重現畫面，但寫入一律拒絕，不論目標使用者有沒有維護權限。
        Assert.Empty((await admin.GetFromJsonAsync<CardDto[]>(Cards))!);
        await AssertCodeAsync(await admin.PostAsJsonAsync(Cards, new CardRequest("X1", CardKinds.Employee, null)), HttpStatusCode.Forbidden, "test_admin_read_only");
        await AssertCodeAsync(await AssignAsync(admin, Guid.NewGuid(), hrId, At(10, 1)), HttpStatusCode.Forbidden, "test_admin_read_only");
    }

    private static async Task RevokeAsync(AppFactory factory, string groupId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Set<RoleGroupFeature>().Where(x => x.GroupId == groupId).ExecuteDeleteAsync();
    }
}
