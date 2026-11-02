using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Features.Admin.AccessControl;
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
        var id = session.Card.Id;
        await using var scope = session.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var write = scope.ServiceProvider.GetRequiredService<AdministrativeWrite>();
        var seen = new List<bool>();
        var target = AuditTarget.Of(id, async ct =>
        {
            var card = await db.Cards.SingleAsync(x => x.Id == id, ct);
            seen.Add(card.HiddenFromRecords);
            return new { card.HiddenFromRecords };
        });

        var begun = await write.BeginAsync(target, CancellationToken.None);
        await using var transaction = begun.Value!;
        await db.Cards.Where(x => x.Id == id).ExecuteUpdateAsync(p => p.SetProperty(x => x.HiddenFromRecords, true));
        var outcome = await transaction.CommitAsync(CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal([false, true], seen);
        Assert.Empty(db.ChangeTracker.Entries<Card>());
        Assert.Equal(QueryTrackingBehavior.TrackAll, db.ChangeTracker.QueryTrackingBehavior);
    }

    // 帳號與角色都會改變權限：開始時不持有就不能寫，變更後不再持有就整筆回滾。
    [Fact]
    public async Task KeepingAccessRefusesAnActorWithoutTheFeatureAndRollsBackWhenTheChangeRemovesIt()
    {
        await using var factory = new AppFactory(administrators: ["alice"]);
        using var admin = await factory.SignedInAsync();
        var actor = await OrganizationApi.UserIdAsync(factory, "alice");
        var ct = TestContext.Current.CancellationToken;
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var write = scope.ServiceProvider.GetRequiredService<AdministrativeWrite>();
        var target = AuditTarget.Of(actor, _ => Task.FromResult<object?>(null));

        var stranger = await write.BeginAsync(target.KeepingAccess(Guid.NewGuid(), FeatureCatalog.IdentityUsers), ct);
        Assert.Equal(AccessControlErrors.FeatureForbidden, stranger.Error);

        var begun = await write.BeginAsync(target.KeepingAccess(actor, FeatureCatalog.IdentityUsers), ct);
        await using var transaction = begun.Value!;
        db.UserRoles.RemoveRange(await db.UserRoles.Where(x => x.UserId == actor).ToListAsync(ct));
        Assert.Equal(AccessControlErrors.Lockout, (await transaction.CommitAsync(ct)).Error);

        await using var check = factory.Services.CreateAsyncScope();
        Assert.True(await check.ServiceProvider.GetRequiredService<AppDbContext>().UserRoles.AnyAsync(x => x.UserId == actor, ct));
    }
}
