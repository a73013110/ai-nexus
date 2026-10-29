using AiNexus.Data.App;
using AiNexus.Data.App.Notifications;
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
        Assert.Equal(("alice", "schema-changes"), (first.Owner, first.TargetKind));

        await ExecuteAsync(factory, "CREATE TABLE [dbo].[Users_bak] ([Id] int NOT NULL);");
        Assert.Equal(2, await watch.CheckAsync(CancellationToken.None));
        Assert.Equal(2, (await NotificationsAsync(factory)).Count);
    }

    private static async Task<List<(string Owner, string TargetKind)>> NotificationsAsync(AppFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return [.. (await (from n in db.Set<Notification>()
                           join u in db.Users on n.OwnerId equals u.Id
                           where n.Type == "releases.schema_changed"
                           select new { u.AdAccount, n.TargetKind }).ToListAsync()).Select(x => (x.AdAccount!, x.TargetKind))];
    }
}
