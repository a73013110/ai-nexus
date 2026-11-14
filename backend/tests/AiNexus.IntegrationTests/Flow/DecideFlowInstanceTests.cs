using System.Net;
using AiNexus.Data.App;
using AiNexus.Data.App.Flow;
using AiNexus.Data.App.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.FlowApi;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.Flow;

public sealed class DecideFlowInstanceTests
{
    [Fact]
    public async Task ManagerThenStewardApprovalGrantsAccessThatSyncWritesToGitea()
    {
        await using var session = await RepositoryAccessSession.StartAsync();
        using var bob = await session.ClientAsync("bob"); using var dave = await session.ClientAsync("dave"); using var carol = await session.ClientAsync("carol");
        var id = await SubmitAsync(session, bob, "write");

        Assert.Equal([id], (await TasksAsync(dave)).Select(x => x.Id));
        Assert.Empty(await TasksAsync(carol));
        await AssertCodeAsync(await DecideAsync(carol, id, 1, true), HttpStatusCode.Forbidden, "flow_not_approver");
        await ApproveAsync(dave, id, 1);
        Assert.Equal([id], (await TasksAsync(carol)).Select(x => x.Id));
        Assert.Empty(await session.GrantsAsync());

        await ApproveAsync(carol, id, 2);
        var instance = await InstanceAsync(bob, id);
        Assert.Equal((FlowStatuses.Approved, null), (instance.Status, instance.CurrentStep));
        Assert.All(instance.Steps, x => Assert.Equal(FlowStepStatuses.Approved, x.Status));
        var grant = Assert.Single(await session.GrantsAsync());
        Assert.Equal((session.Users["bob"], RepositoryPermissions.Write, RepositoryGrantSources.Request, RepositorySyncStates.Pending),
            (grant.UserId, grant.Permission, grant.Source, grant.SyncState));

        await session.SyncAsync();
        Assert.Equal("write", session.Gitea.CollaboratorsOf(RepositoryAccessSession.Nexus)["bob"]);
        Assert.Equal(RepositorySyncStates.Applied, Assert.Single(await session.GrantsAsync()).SyncState);
        using var scope = session.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.Notifications.AnyAsync(x => x.OwnerId == session.Users["bob"] && x.Type == "flow.completed" && x.TargetId == id));
        Assert.True(await db.Notifications.AnyAsync(x => x.OwnerId == session.Users["dave"] && x.Type == "flow.assigned"));
        Assert.False(await db.FlowAssignments.AnyAsync());
    }

    [Fact]
    public async Task RejectionStopsTheRemainingStepsAndGrantsNothing()
    {
        await using var session = await RepositoryAccessSession.StartAsync();
        using var bob = await session.ClientAsync("bob"); using var dave = await session.ClientAsync("dave"); using var carol = await session.ClientAsync("carol");
        var id = await SubmitAsync(session, bob);

        Assert.True((await DecideAsync(dave, id, 1, false, "目前專案不需要")).IsSuccessStatusCode);

        var instance = await InstanceAsync(bob, id);
        Assert.Equal(FlowStatuses.Rejected, instance.Status);
        Assert.Equal([FlowStepStatuses.Rejected, FlowStepStatuses.Stopped], instance.Steps.Select(x => x.Status));
        Assert.Equal("目前專案不需要", instance.Steps[0].Comment);
        Assert.Empty(await TasksAsync(carol));
        Assert.Empty(await session.GrantsAsync());
        await AssertCodeAsync(await DecideAsync(dave, id, 1, true), HttpStatusCode.Conflict, "flow_instance_closed");
    }

    [Fact]
    public async Task DecisionForAStepThatAlreadyMovedOnIsRefused()
    {
        await using var session = await RepositoryAccessSession.StartAsync();
        using var bob = await session.ClientAsync("bob"); using var dave = await session.ClientAsync("dave");
        var id = await SubmitAsync(session, bob);

        var decisions = await Task.WhenAll(DecideAsync(dave, id, 1, true), DecideAsync(dave, id, 1, true));

        Assert.Single(decisions, x => x.IsSuccessStatusCode);
        await AssertCodeAsync(decisions.Single(x => !x.IsSuccessStatusCode), HttpStatusCode.Conflict, "flow_instance_changed");
        Assert.Equal(2, (await InstanceAsync(bob, id)).CurrentStep);
    }

    [Fact]
    public async Task ApproverOfAnEarlierStepPassesTheirLaterStepsAutomatically()
    {
        await using var session = await RepositoryAccessSession.StartAsync();
        using (var scope = session.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.RepositoryStewards.Add(new RepositorySteward { RepositoryId = session.Repository, UserId = session.Users["dave"] });
            await db.SaveChangesAsync();
        }
        using var bob = await session.ClientAsync("bob"); using var dave = await session.ClientAsync("dave");
        var id = await SubmitAsync(session, bob);

        await ApproveAsync(dave, id, 1);

        var instance = await InstanceAsync(bob, id);
        Assert.Equal(FlowStatuses.Approved, instance.Status);
        Assert.Equal((FlowStepStatuses.Approved, FlowStepNotes.AlreadyApproved), (instance.Steps[1].Status, instance.Steps[1].Note));
        Assert.Single(await session.GrantsAsync());
    }

    [Fact]
    public async Task AdministratorsDecideOnBehalfAndApplicantsNeverDecideTheirOwn()
    {
        await using var session = await RepositoryAccessSession.StartAsync();
        using var bob = await session.ClientAsync("bob"); using var alice = await session.ClientAsync("alice");
        var id = await SubmitAsync(session, bob);

        await AssertCodeAsync(await DecideAsync(bob, id, 1, true), HttpStatusCode.Forbidden, "flow_own_request");
        var before = await InstanceAsync(alice, id);
        Assert.True(before.CanDecide && before.OnBehalf);
        await ApproveAsync(alice, id, 1);

        var step = (await InstanceAsync(bob, id)).Steps[0];
        Assert.True(step.OnBehalf);
        Assert.Equal(session.Users["alice"], step.Decider!.Id);
    }
}
