using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.AccessControl;

public sealed class AdministrativeWriteTests
{
    // 變更繞過追蹤（ExecuteUpdate、procedure）時，追蹤中的實體不會更新：快照漏寫 AsNoTracking 也不能記到舊值。
    [Fact]
    public async Task SnapshotsSeeTheDatabaseEvenWhenTheyForgetNoTrackingAndTheChangeBypassesTheTracker()
    {
        await using var session = await HrSession.CreateAsync();
        var actor = await OrganizationApi.UserIdAsync(session.Factory, "hr");
        var id = session.Card.Id;
        await using var scope = session.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var write = scope.ServiceProvider.GetRequiredService<AdministrativeWrite>();
        var seen = new List<bool>();
        var target = AuditTarget.Of(FeatureCatalog.AttendanceCardsMaintain, id, async ct =>
        {
            var card = await db.Cards.SingleAsync(x => x.Id == id, ct);
            seen.Add(card.HiddenFromRecords);
            return new { card.HiddenFromRecords };
        });

        var outcome = await write.ExecuteAsync(actor, target, async () =>
        {
            await db.Cards.Where(x => x.Id == id).ExecuteUpdateAsync(p => p.SetProperty(x => x.HiddenFromRecords, true));
            return Result.Success;
        }, CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal([false, true], seen);
        Assert.Empty(db.ChangeTracker.Entries<Card>());
        Assert.Equal(QueryTrackingBehavior.TrackAll, db.ChangeTracker.QueryTrackingBehavior);
    }
}
