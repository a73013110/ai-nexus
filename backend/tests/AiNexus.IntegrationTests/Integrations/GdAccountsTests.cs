using AiNexus.Features.Admin.Integrations.Gd;

namespace AiNexus.IntegrationTests.Integrations;

public sealed class GdAccountsTests
{
    [Fact]
    public async Task AUserMapsToTheGdAccountsWhoseAccountMatchesTheirAdAccountIgnoringCase()
    {
        await FakeGd.WithAppAsync(async (gd, db) =>
        {
            gd.AddUser("G001", "alice");
            gd.AddUser("G003", "alice");
            gd.AddUser("G002", "bob");
            var alice = FakeGd.AddAppUser(db, "ALICE");
            var bob = FakeGd.AddAppUser(db, " bob ");

            var map = await new GdAccounts(db, gd.Access()).ResolveAsync([alice, bob, alice], TestContext.Current.CancellationToken);

            Assert.NotNull(map);
            Assert.Equal(["G001", "G003"], map.GdIds[alice].Order(StringComparer.Ordinal));
            Assert.Equal(["G002"], map.GdIds[bob]);
            Assert.Empty(map.Unmapped);
        });
    }

    [Fact]
    public async Task DisabledGdAccountsAreDroppedUnlessNoneIsEnabled()
    {
        await FakeGd.WithAppAsync(async (gd, db) =>
        {
            gd.AddUser("G001", "alice", disabled: true);
            gd.AddUser("G002", "alice");
            gd.AddUser("G003", "bob", disabled: null);
            gd.AddUser("G004", "bob", disabled: true);
            gd.AddUser("G005", "carol", disabled: true);
            gd.AddUser("G006", "carol", disabled: true);
            gd.AddUser("G007", "dave", disabled: true);
            var alice = FakeGd.AddAppUser(db, "alice");
            var bob = FakeGd.AddAppUser(db, "bob");
            var carol = FakeGd.AddAppUser(db, "carol");
            var dave = FakeGd.AddAppUser(db, "dave");

            var map = await new GdAccounts(db, gd.Access()).ResolveAsync([alice, bob, carol, dave], TestContext.Current.CancellationToken);

            Assert.NotNull(map);
            Assert.Equal(["G002"], map.GdIds[alice]);
            // Disable 為 null 視為啟用。
            Assert.Equal(["G003"], map.GdIds[bob]);
            // 全部停用時退回全部列，已停用人員的歷史資料仍對得到。
            Assert.Equal(["G005", "G006"], map.GdIds[carol].Order(StringComparer.Ordinal));
            Assert.Equal(["G007"], map.GdIds[dave]);
            Assert.Empty(map.Unmapped);
        });
    }

    [Fact]
    public async Task UsersWithoutAnAdAccountOrAGdAccountOrWhoDoNotExistAreUnmapped()
    {
        await FakeGd.WithAppAsync(async (gd, db) =>
        {
            gd.AddUser("G001", "alice");
            var alice = FakeGd.AddAppUser(db, "alice");
            var noAccount = FakeGd.AddAppUser(db, null);
            var blank = FakeGd.AddAppUser(db, "  ");
            var notInGd = FakeGd.AddAppUser(db, "carol");
            var unknown = Guid.NewGuid();

            var map = await new GdAccounts(db, gd.Access()).ResolveAsync([alice, noAccount, blank, notInGd, unknown], TestContext.Current.CancellationToken);

            Assert.NotNull(map);
            Assert.Equal([alice], map.GdIds.Keys);
            Assert.Equal(new[] { noAccount, blank, notInGd, unknown }.Order(), map.Unmapped.Order());
        });
    }

    [Fact]
    public async Task NoUsersMapToNothingWithoutQueryingGd()
    {
        await TestDatabase.WithContextAsync(async db =>
        {
            var map = await new GdAccounts(db, FakeGd.AccessTo(FakeGd.Unreachable())).ResolveAsync([], TestContext.Current.CancellationToken);

            Assert.NotNull(map);
            Assert.Empty(map.GdIds);
            Assert.Empty(map.Unmapped);
        });
    }

    [Fact]
    public async Task AnUnavailableGdYieldsNoMapInsteadOfEveryoneUnmapped()
    {
        await TestDatabase.WithContextAsync(async db =>
        {
            var alice = FakeGd.AddAppUser(db, "alice");

            var map = await new GdAccounts(db, FakeGd.AccessTo(FakeGd.Unreachable())).ResolveAsync([alice], TestContext.Current.CancellationToken);

            Assert.Null(map);
        });
    }
}
