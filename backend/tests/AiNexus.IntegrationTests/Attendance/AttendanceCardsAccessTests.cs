using System.Net;
using System.Net.Http.Json;
using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Features.Office.Attendance.Cards;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.AttendanceApi;
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
            await member.PostAsJsonAsync(Cards, new CardCreateRequest("X1", false, null)),
            await member.PutAsJsonAsync($"{Cards}/{session.Card.Id}", new CardUpdateRequest(true, null)),
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

        foreach (var response in new[]
        {
            await viewer.PostAsJsonAsync(Cards, new CardCreateRequest("X1", false, null)),
            await viewer.PutAsJsonAsync($"{Cards}/{session.Card.Id}", new CardUpdateRequest(true, null)),
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
        await AssertCodeAsync(await session.Hr.PostAsJsonAsync(Cards, new CardCreateRequest("X1", false, null)), HttpStatusCode.Forbidden, "feature_forbidden");
    }

    private static async Task RevokeAsync(AppFactory factory, string groupId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Set<RoleGroupFeature>().Where(x => x.GroupId == groupId).ExecuteDeleteAsync();
    }
}
