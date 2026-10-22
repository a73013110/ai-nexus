using AiNexus.Data.App;
using AiNexus.Features.Staff.Attendance.Gd;
using AiNexus.Features.Staff.Attendance.Leave;
using AiNexus.Platform.Data;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.Attendance;

public sealed class LeaveSheetReaderTests
{
    private static readonly DateOnly Oct6 = new(2026, 10, 6);
    private static readonly DateOnly Oct8 = new(2026, 10, 8);

    private static DateTime At(int day, int hour = 0, int minute = 0) => new(2026, 10, day, hour, minute, 0);

    private static DateTimeOffset Taipei(int day, int hour, int minute = 0) => new(2026, 10, day, hour, minute, 0, TimeSpan.FromHours(8));

    /// <summary>App 資料庫（使用者）與假 GD 各一個 LocalDB 資料庫；<see cref="Gd"/> 預設有 alice（G001）、bob（G002）兩個公文帳號。</summary>
    private sealed class Scenario(AppDbContext db, FakeGd gd)
    {
        public FakeGd Gd { get; } = gd;

        public Guid AddUser(string? adAccount) => FakeGd.AddAppUser(db, adAccount);

        public Task<LeaveLookup> ReadAsync(DateOnly from, DateOnly to, DatabasesOptions? source = null, params Guid[] users)
        {
            var access = source is null ? Gd.Access() : FakeGd.AccessTo(source);
            return new LeaveSheetReader(new GdAccounts(db, access), access).ReadAsync(users, from, to, TestContext.Current.CancellationToken);
        }
    }

    private static Task RunAsync(Func<Scenario, Task> test) => FakeGd.WithAppAsync((gd, db) =>
    {
        gd.AddUser("G001", "alice");
        gd.AddUser("G002", "bob");
        return test(new Scenario(db, gd));
    });

    [Fact]
    public async Task OnlySentDecidedAndFinishedSheetsAreReturned()
    {
        await RunAsync(async s =>
        {
            var alice = s.AddUser("alice");
            s.Gd.AddKind("A", "特休");
            foreach (var flow in new[] { "Temp", "Back", "Cancel", "Send", "Decide", "Finish" })
                s.Gd.AddLeave("L-" + flow, "G001", "A", At(6, 9), At(6, 12), flow);
            foreach (var flow in new[] { "Temp", "Cancel", "Finish" }) s.Gd.AddTravel("T-" + flow, "G001", At(7, 9), At(7, 17), "拜訪客戶", flow);

            var lookup = await s.ReadAsync(Oct6, Oct8, users: alice);

            Assert.True(lookup.Available);
            Assert.Equal(["L-Decide", "L-Finish", "L-Send", "T-Finish"], lookup.For(alice).Select(x => x.DesNo).Order(StringComparer.Ordinal));
            Assert.Equal(["Decide", "Finish", "Finish", "Send"], lookup.For(alice).Select(x => x.FlowStatus).Order(StringComparer.Ordinal));
        });
    }

    [Fact]
    public async Task ALeaveSheetCarriesTheKindNameAndTaipeiTimes()
    {
        await RunAsync(async s =>
        {
            var alice = s.AddUser("alice");
            s.Gd.AddKind("A", "特休");
            s.Gd.AddLeave("L-1", "G001", "A", At(6, 8, 30), At(6, 12), "Finish");
            s.Gd.AddLeave("L-2", "G001", "Z", At(7, 13, 30), At(7, 18), "Send");

            var sheets = (await s.ReadAsync(Oct6, Oct8, users: alice)).For(alice);

            Assert.Equal(new LeaveSheet("L-1", LeaveSheetKind.Leave, "特休", Taipei(6, 8, 30), Taipei(6, 12), "Finish"), sheets[0]);
            // 假別代碼不在 Form_LeaveKind 時退回代碼，不留空白。
            Assert.Equal(new LeaveSheet("L-2", LeaveSheetKind.Leave, "Z", Taipei(7, 13, 30), Taipei(7, 18), "Send"), sheets[1]);
            Assert.Equal(TimeSpan.FromHours(8), sheets[0].StartsAt.Offset);
        });
    }

    [Fact]
    public async Task ATravelSheetUsesTheTravelPurposeAsItsTitle()
    {
        await RunAsync(async s =>
        {
            var alice = s.AddUser("alice");
            s.Gd.AddTravel("T-1", "G001", At(7, 9), At(7, 17), "拜訪南部客戶", "Decide");

            var sheet = Assert.Single((await s.ReadAsync(Oct6, Oct8, users: alice)).For(alice));

            Assert.Equal(new LeaveSheet("T-1", LeaveSheetKind.Travel, "拜訪南部客戶", Taipei(7, 9), Taipei(7, 17), "Decide"), sheet);
        });
    }

    [Fact]
    public async Task OnlySheetsOverlappingTheRangeAreReturned()
    {
        await RunAsync(async s =>
        {
            var alice = s.AddUser("alice");
            s.Gd.AddKind("A", "特休");
            s.Gd.AddLeave("before", "G001", "A", At(5, 9), At(5, 18), "Finish");
            s.Gd.AddLeave("ends-at-start", "G001", "A", At(5, 13), At(6), "Finish");
            s.Gd.AddLeave("crosses-start", "G001", "A", At(5, 13), At(6, 10), "Finish");
            s.Gd.AddLeave("inside", "G001", "A", At(7, 9), At(7, 12), "Finish");
            s.Gd.AddLeave("crosses-end", "G001", "A", At(8, 13), At(9, 10), "Finish");
            s.Gd.AddLeave("starts-at-end", "G001", "A", At(9), At(9, 12), "Finish");
            s.Gd.AddLeave("after", "G001", "A", At(12, 9), At(12, 18), "Finish");

            var sheets = (await s.ReadAsync(Oct6, Oct8, users: alice)).For(alice);

            Assert.Equal(["crosses-start", "inside", "crosses-end"], sheets.Select(x => x.DesNo));
        });
    }

    [Fact]
    public async Task ALeaveWithADayOfWeekUsesItsGroupRowsAndTheirOverlapWithTheRange()
    {
        await RunAsync(async s =>
        {
            var alice = s.AddUser("alice");
            s.Gd.AddKind("A", "特休");
            // 十月整月的主單，實際只有 6 日與 20 日請假；主單的起訖不能拿來判斷。
            s.Gd.AddLeave("L-multi", "G001", "A", At(1, 8, 30), At(30, 18), "Finish", dayOfWeek: "2,2");
            s.Gd.AddLeaveGroup("L-multi", At(6, 8, 30), At(6, 18), "2");
            s.Gd.AddLeaveGroup("L-multi", At(20, 8, 30), At(20, 18), "2");
            s.Gd.AddLeave("L-plain", "G001", "A", At(7, 9), At(7, 12), "Finish", dayOfWeek: "  ");
            // 抽單的跨區間假單，各段也不算。
            s.Gd.AddLeave("L-cancelled", "G001", "A", At(1, 8, 30), At(30, 18), "Cancel", dayOfWeek: "3");
            s.Gd.AddLeaveGroup("L-cancelled", At(7, 13, 30), At(7, 18), "3");

            var week = (await s.ReadAsync(Oct6, Oct8, users: alice)).For(alice);
            var gap = (await s.ReadAsync(new(2026, 10, 7), new(2026, 10, 7), users: alice)).For(alice);

            Assert.Equal([("L-multi", Taipei(6, 8, 30), Taipei(6, 18)), ("L-plain", Taipei(7, 9), Taipei(7, 12))], week.Select(x => (x.DesNo, x.StartsAt, x.EndsAt)));
            Assert.Equal(["L-plain"], gap.Select(x => x.DesNo));
        });
    }

    [Fact]
    public async Task EachUserGetsOnlyTheirOwnSheetsAndUnmappedUsersAreReported()
    {
        await RunAsync(async s =>
        {
            var alice = s.AddUser("ALICE");
            var bob = s.AddUser("bob");
            var noAccount = s.AddUser(null);
            var notInGd = s.AddUser("carol");
            var unknown = Guid.NewGuid();
            s.Gd.AddKind("A", "特休");
            s.Gd.AddLeave("L-1", "G001", "A", At(6, 9), At(6, 12), "Finish");
            s.Gd.AddLeave("L-2", "G002", "A", At(7, 9), At(7, 12), "Finish");

            var lookup = await s.ReadAsync(Oct6, Oct8, users: [alice, bob, noAccount, notInGd, unknown]);

            Assert.True(lookup.Available);
            Assert.Equal(["L-1"], lookup.For(alice).Select(x => x.DesNo));
            Assert.Equal(["L-2"], lookup.For(bob).Select(x => x.DesNo));
            Assert.Empty(lookup.For(noAccount));
            Assert.Equal(new[] { noAccount, notInGd, unknown }.Order(), lookup.Unmapped.Order());
        });
    }

    [Fact]
    public async Task AUserWhoseAdAccountHasSeveralGdAccountsGetsAllTheirSheets()
    {
        await RunAsync(async s =>
        {
            var alice = s.AddUser("alice");
            s.Gd.AddUser("G003", "alice");
            s.Gd.AddKind("A", "特休");
            s.Gd.AddLeave("L-1", "G001", "A", At(6, 9), At(6, 12), "Finish");
            s.Gd.AddLeave("L-3", "G003", "A", At(7, 9), At(7, 12), "Finish");

            var lookup = await s.ReadAsync(Oct6, Oct8, users: alice);

            Assert.Equal(["L-1", "L-3"], lookup.For(alice).Select(x => x.DesNo));
        });
    }

    [Fact]
    public async Task ADisabledGdAccountIsReadOnlyWhenItIsTheOnlyOne()
    {
        await RunAsync(async s =>
        {
            var carol = s.AddUser("carol");
            var dave = s.AddUser("dave");
            s.Gd.AddUser("G010", "carol", disabled: true);
            s.Gd.AddUser("G011", "dave", disabled: true);
            s.Gd.AddUser("G012", "dave");
            s.Gd.AddKind("A", "特休");
            s.Gd.AddLeave("L-10", "G010", "A", At(6, 9), At(6, 12), "Finish");
            s.Gd.AddLeave("L-11", "G011", "A", At(6, 9), At(6, 12), "Finish");
            s.Gd.AddLeave("L-12", "G012", "A", At(7, 9), At(7, 12), "Finish");

            var lookup = await s.ReadAsync(Oct6, Oct8, users: [carol, dave]);

            // 已停用人員只剩停用帳號，歷史假單照常查得到；有啟用帳號時只讀啟用的那個。
            Assert.Equal(["L-10"], lookup.For(carol).Select(x => x.DesNo));
            Assert.Equal(["L-12"], lookup.For(dave).Select(x => x.DesNo));
            Assert.Empty(lookup.Unmapped);
        });
    }

    [Fact]
    public async Task AMappedUserWithoutSheetsIsNotUnmapped()
    {
        await RunAsync(async s =>
        {
            var alice = s.AddUser("alice");

            var lookup = await s.ReadAsync(Oct6, Oct8, users: alice);

            Assert.True(lookup.Available);
            Assert.Empty(lookup.Unmapped);
            Assert.Empty(lookup.For(alice));
        });
    }

    [Fact]
    public async Task AnUnreachableOrUnconfiguredGdDegradesToUnavailable()
    {
        await RunAsync(async s =>
        {
            var alice = s.AddUser("alice");

            foreach (var source in new[] { FakeGd.Unreachable(), FakeGd.NotConfigured() })
            {
                var lookup = await s.ReadAsync(Oct6, Oct8, source, alice);

                Assert.False(lookup.Available);
                Assert.Empty(lookup.Sheets);
                Assert.Empty(lookup.Unmapped);
            }
        });
    }

    [Fact]
    public async Task NoUsersMeansNoQueryAgainstGd()
    {
        await RunAsync(async s => Assert.True((await s.ReadAsync(Oct6, Oct8, FakeGd.Unreachable())).Available));
    }

    [Fact]
    public async Task ARangeThatEndsBeforeItStartsIsRejected()
    {
        await RunAsync(s => Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => s.ReadAsync(Oct8, Oct6)));
    }

    [Fact]
    public async Task TheHostRegistersTheReaderAndADeploymentWithoutGdStillGetsALookup()
    {
        await using var factory = new AppFactory();
        using var scope = factory.Services.CreateScope();
        var alice = FakeGd.AddAppUser(scope.ServiceProvider.GetRequiredService<AppDbContext>(), "alice");

        var lookup = await scope.ServiceProvider.GetRequiredService<LeaveSheetReader>().ReadAsync([alice], Oct6, Oct8, TestContext.Current.CancellationToken);

        Assert.False(lookup.Available);
    }
}
