using AiNexus.Data.App;
using AiNexus.Data.App.Flow;
using AiNexus.Features.Office.Flow.Maintenance;
using AiNexus.Platform.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using static AiNexus.IntegrationTests.Support.FlowApi;

namespace AiNexus.IntegrationTests.Flow;

public sealed class FlowMaintenanceTaskTests
{
    private static Task<TaskOutcome> RunAsync(RepositoryAccessSession session) => session.Factory.InScopeAsync((FlowMaintenanceTask task)
        => task.RunAsync(new ScheduledTaskContext(ScheduledTaskTrigger.Manual, null, null), CancellationToken.None));

    [Fact]
    public async Task PendingStepsFollowOrganizationChangesAndExpireAfterTheConfiguredDays()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var session = await RepositoryAccessSession.StartAsync(clock);
        using var bob = await session.ClientAsync("bob"); using var dave = await session.ClientAsync("dave"); using var carol = await session.ClientAsync("carol");
        var id = await SubmitAsync(session, bob);

        // 主管換人：dave 不再是主管、carol 接任。
        using (var scope = session.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var unit = await db.UnitMembers.Where(x => x.UserId == session.Users["dave"]).Select(x => x.UnitId).SingleAsync();
            await db.UnitMembers.Where(x => x.UserId == session.Users["dave"]).ExecuteUpdateAsync(x => x.SetProperty(y => y.IsManager, false));
            db.UnitMembers.Add(new() { UnitId = unit, UserId = session.Users["carol"], IsPrimary = true, IsManager = true });
            await db.SaveChangesAsync();
        }
        await RunAsync(session);

        Assert.Empty(await TasksAsync(dave));
        Assert.Equal([id], (await TasksAsync(carol)).Select(x => x.Id));
        Assert.Equal(FlowStatuses.Pending, (await InstanceAsync(bob, id)).Status);

        clock.Advance(TimeSpan.FromDays(31));
        await RunAsync(session);

        Assert.Equal(FlowStatuses.Expired, (await InstanceAsync(bob, id)).Status);
        Assert.Empty(await TasksAsync(carol));
        Assert.Empty(await session.GrantsAsync());
    }
}
