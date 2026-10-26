using AiNexus.Data.App;
using AiNexus.Features.Office.Attendance.Forms;
using AiNexus.Features.Platform.Gd;
using AiNexus.Platform.Data;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.Attendance;

public sealed class GdFormReaderTests
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

        public Task<GdFormLookup> ReadAsync(DateOnly from, DateOnly to, DatabasesOptions? source = null, params Guid[] users)
        {
            var access = source is null ? Gd.Access() : FakeGd.AccessTo(source);
            return new GdFormReader(new GdAccounts(db, access), access).ReadAsync(users, from, to, TestContext.Current.CancellationToken);
        }
    }

    private static Task RunAsync(Func<Scenario, Task> test) => FakeGd.WithAppAsync((gd, db) =>
    {
        gd.AddUser("G001", "alice");
        gd.AddUser("G002", "bob");
        return test(new Scenario(db, gd));
    });

    [Fact]
    public async Task OnlyApprovedAndInFlowFormsAreReturned()
    {
        await RunAsync(async s =>
        {
            var alice = s.AddUser("alice");
            s.Gd.AddKind("A", "特休");
            foreach (var flow in new[] { "Temp", "Back", "Cancel", "Send", "Decide", "Finish" })
                s.Gd.AddLeave("L-" + flow, "G001", "A", At(6, 9), At(6, 12), flow);
            // Finish 是退回結案（承辦人把退回的單結案存查），不是核准，也不算有效表單。
            foreach (var flow in new[] { "Temp", "Cancel", "Finish", "Decide" }) s.Gd.AddTravel("T-" + flow, "G001", At(7, 9), At(7, 17), "拜訪客戶", flow);

            var lookup = await s.ReadAsync(Oct6, Oct8, users: alice);

            Assert.True(lookup.Available);
            Assert.Equal(["L-Decide", "L-Send", "T-Decide"], lookup.For(alice).Select(x => x.DesNo).Order(StringComparer.Ordinal));
            Assert.Equal([GdFormFlowOver.Decide, GdFormFlowOver.Send, GdFormFlowOver.Decide], lookup.For(alice).OrderBy(x => x.DesNo, StringComparer.Ordinal).Select(x => x.FlowStatus));
            Assert.Equal([true, false, true], lookup.For(alice).OrderBy(x => x.DesNo, StringComparer.Ordinal).Select(x => x.Approved));
        });
    }

    [Fact]
    public async Task AnInFlowFormCarriesItsCurrentSignerAndStep()
    {
        await RunAsync(async s =>
        {
            var alice = s.AddUser("alice");
            s.Gd.AddUser("G009", "carol", name: "王小明");
            s.Gd.AddLeave("L-1", "G001", "A", At(6, 9), At(6, 12), "Send");
            s.Gd.AddFlowStep("L-1", "003", "2_G002", "請假代理人");
            // 同一單停在多關時取最後一關。
            s.Gd.AddFlowStep("L-1", "004", "7_G009", "核准關卡");
            s.Gd.AddLeave("L-2", "G001", "A", At(7, 9), At(7, 12), "Send");
            // 核准的單不帶目前關卡，即使 FLOW_CURRENT 還留著列。
            s.Gd.AddLeave("L-3", "G001", "A", At(8, 9), At(8, 12), "Decide");
            s.Gd.AddFlowStep("L-3", "009", "7_G009", "流程終結");

            var forms = (await s.ReadAsync(Oct6, Oct8, users: alice)).For(alice);

            Assert.Equal([("王小明", "核准關卡"), (null, null), (null, null)], forms.Select(x => (x.CurrentSigner, x.CurrentStep)));
        });
    }

    [Fact]
    public async Task ALeaveFormCarriesTheKindNameAndTaipeiTimes()
    {
        await RunAsync(async s =>
        {
            var alice = s.AddUser("alice");
            s.Gd.AddKind("A", "特休");
            s.Gd.AddLeave("L-1", "G001", "A", At(6, 8, 30), At(6, 12), "Decide");
            s.Gd.AddLeave("L-2", "G001", "Z", At(7, 13, 30), At(7, 18), "Send");

            var forms = (await s.ReadAsync(Oct6, Oct8, users: alice)).For(alice);

            Assert.Equal(new GdForm("L-1", GdFormKind.Leave, "特休", Taipei(6, 8, 30), Taipei(6, 12), GdFormFlowOver.Decide), forms[0]);
            // 假別代碼不在 Form_LeaveKind 時退回代碼，不留空白。
            Assert.Equal(new GdForm("L-2", GdFormKind.Leave, "Z", Taipei(7, 13, 30), Taipei(7, 18), GdFormFlowOver.Send), forms[1]);
            Assert.Equal(TimeSpan.FromHours(8), forms[0].StartsAt.Offset);
        });
    }

    [Fact]
    public async Task ATravelFormUsesTheTravelPurposeAsItsTitle()
    {
        await RunAsync(async s =>
        {
            var alice = s.AddUser("alice");
            s.Gd.AddTravel("T-1", "G001", At(7, 9), At(7, 17), "拜訪南部客戶", "Decide");

            var form = Assert.Single((await s.ReadAsync(Oct6, Oct8, users: alice)).For(alice));

            Assert.Equal(new GdForm("T-1", GdFormKind.Travel, "拜訪南部客戶", Taipei(7, 9), Taipei(7, 17), GdFormFlowOver.Decide), form);
        });
    }

    [Fact]
    public async Task OnlyFormsOverlappingTheRangeAreReturned()
    {
        await RunAsync(async s =>
        {
            var alice = s.AddUser("alice");
            s.Gd.AddKind("A", "特休");
            s.Gd.AddLeave("before", "G001", "A", At(5, 9), At(5, 18), "Decide");
            s.Gd.AddLeave("ends-at-start", "G001", "A", At(5, 13), At(6), "Decide");
            s.Gd.AddLeave("crosses-start", "G001", "A", At(5, 13), At(6, 10), "Decide");
            s.Gd.AddLeave("inside", "G001", "A", At(7, 9), At(7, 12), "Decide");
            s.Gd.AddLeave("crosses-end", "G001", "A", At(8, 13), At(9, 10), "Decide");
            s.Gd.AddLeave("starts-at-end", "G001", "A", At(9), At(9, 12), "Decide");
            s.Gd.AddLeave("after", "G001", "A", At(12, 9), At(12, 18), "Decide");

            var forms = (await s.ReadAsync(Oct6, Oct8, users: alice)).For(alice);

            Assert.Equal(["crosses-start", "inside", "crosses-end"], forms.Select(x => x.DesNo));
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
            s.Gd.AddLeave("L-multi", "G001", "A", At(1, 8, 30), At(30, 18), "Decide", dayOfWeek: "2,2");
            s.Gd.AddLeaveGroup("L-multi", At(6, 8, 30), At(6, 18), "2");
            s.Gd.AddLeaveGroup("L-multi", At(20, 8, 30), At(20, 18), "2");
            s.Gd.AddLeave("L-plain", "G001", "A", At(7, 9), At(7, 12), "Decide", dayOfWeek: "  ");
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
    public async Task EachUserGetsOnlyTheirOwnFormsAndUnmappedUsersAreReported()
    {
        await RunAsync(async s =>
        {
            var alice = s.AddUser("ALICE");
            var bob = s.AddUser("bob");
            var noAccount = s.AddUser(null);
            var notInGd = s.AddUser("carol");
            var unknown = Guid.NewGuid();
            s.Gd.AddKind("A", "特休");
            s.Gd.AddLeave("L-1", "G001", "A", At(6, 9), At(6, 12), "Decide");
            s.Gd.AddLeave("L-2", "G002", "A", At(7, 9), At(7, 12), "Decide");

            var lookup = await s.ReadAsync(Oct6, Oct8, users: [alice, bob, noAccount, notInGd, unknown]);

            Assert.True(lookup.Available);
            Assert.Equal(["L-1"], lookup.For(alice).Select(x => x.DesNo));
            Assert.Equal(["L-2"], lookup.For(bob).Select(x => x.DesNo));
            Assert.Empty(lookup.For(noAccount));
            Assert.Equal(new[] { noAccount, notInGd, unknown }.Order(), lookup.Unmapped.Order());
        });
    }

    [Fact]
    public async Task AUserWhoseAdAccountHasSeveralGdAccountsGetsAllTheirForms()
    {
        await RunAsync(async s =>
        {
            var alice = s.AddUser("alice");
            s.Gd.AddUser("G003", "alice");
            s.Gd.AddKind("A", "特休");
            s.Gd.AddLeave("L-1", "G001", "A", At(6, 9), At(6, 12), "Decide");
            s.Gd.AddLeave("L-3", "G003", "A", At(7, 9), At(7, 12), "Decide");

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
            s.Gd.AddLeave("L-10", "G010", "A", At(6, 9), At(6, 12), "Decide");
            s.Gd.AddLeave("L-11", "G011", "A", At(6, 9), At(6, 12), "Decide");
            s.Gd.AddLeave("L-12", "G012", "A", At(7, 9), At(7, 12), "Decide");

            var lookup = await s.ReadAsync(Oct6, Oct8, users: [carol, dave]);

            // 已停用人員只剩停用帳號，歷史假單照常查得到；有啟用帳號時只讀啟用的那個。
            Assert.Equal(["L-10"], lookup.For(carol).Select(x => x.DesNo));
            Assert.Equal(["L-12"], lookup.For(dave).Select(x => x.DesNo));
            Assert.Empty(lookup.Unmapped);
        });
    }

    [Fact]
    public async Task AMappedUserWithoutFormsIsNotUnmapped()
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
                Assert.Empty(lookup.Forms);
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

        var lookup = await scope.ServiceProvider.GetRequiredService<GdFormReader>().ReadAsync([alice], Oct6, Oct8, TestContext.Current.CancellationToken);

        Assert.False(lookup.Available);
    }
}
