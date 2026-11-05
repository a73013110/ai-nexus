using AiNexus.Data.App;
using AiNexus.Features.Admin.Releases.SchemaChanges;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.ReleaseFixtures;

namespace AiNexus.IntegrationTests.Releases;

public sealed class SchemaChangeWatchTests
{
    [Fact]
    public async Task VersionPageHoldersAreNotifiedOncePerNewSetOfChanges()
    {
        await using var factory = new AppFactory(administrators: ["alice"]);
        using (await factory.SignedInAsync()) { }
        using (await factory.SignedInAsync("bob")) { }
        var watch = ActivatorUtilities.CreateInstance<SchemaChangeWatch>(factory.Services);

        Assert.Equal(0, await watch.CheckAsync(CancellationToken.None));
        await SnapshotAsync(factory);
        Assert.Equal(0, await watch.CheckAsync(CancellationToken.None));
        Assert.Empty(await NotificationsAsync(factory));

        await HotfixProcedureAsync(factory, "operations", "usp_ListAdminUsers");
        Assert.Equal(1, await watch.CheckAsync(CancellationToken.None));
        Assert.Equal(1, await watch.CheckAsync(CancellationToken.None));
        // 只有版本頁的持有者（alice）收到，同一份異動清單不重複通知。
        var first = Assert.Single(await NotificationsAsync(factory));
        Assert.Equal(("alice", "schema-changes", false), first);

        await ExecuteAsync(factory, "CREATE TABLE [dbo].[Users_bak] ([Id] int NOT NULL);");
        Assert.Equal(2, await watch.CheckAsync(CancellationToken.None));
        // 新的異動清單取代舊的：兩則都在，但只有最新一則未讀。
        Assert.Equal([true, false], (await NotificationsAsync(factory)).Select(x => x.Read));

        // 異動回收（快照跟上目前結構）後，提醒自動撤下。
        await SnapshotAsync(factory);
        Assert.Equal(0, await watch.CheckAsync(CancellationToken.None));
        Assert.All(await NotificationsAsync(factory), x => Assert.True(x.Read));
    }

    private static async Task<List<(string Owner, string TargetKind, bool Read)>> NotificationsAsync(AppFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return [.. (await (from n in db.Notifications
                           join u in db.Users on n.OwnerId equals u.Id
                           where n.Type == "releases.schema_changed"
                           orderby n.CreatedAt, n.Id
                           select new { u.AdAccount, n.TargetKind, n.ReadAt }).ToListAsync()).Select(x => (x.AdAccount!, x.TargetKind, x.ReadAt != null))];
    }
}
