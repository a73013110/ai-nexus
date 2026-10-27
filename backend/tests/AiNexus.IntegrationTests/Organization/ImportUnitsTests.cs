using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Organization;
using AiNexus.Features.Admin.Organization.Imports;
using AiNexus.Features.Platform.Gd;
using AiNexus.Platform.Data;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.IdentityApi;
using static AiNexus.IntegrationTests.Support.OrganizationApi;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.Organization;

public sealed class ImportUnitsTests
{
    private const string AuditAction = "admin.organization_import";

    [Fact]
    public async Task TheFirstImportBuildsTheTreeAndManagersAndReportsWhatItSkipped()
    {
        await using var session = await ImportSession.CreateAsync();
        var gd = session.Gd;
        var alice = await session.PersonAsync("alice", "G-ADM");
        var bob = await session.PersonAsync("bob", "G-BOB");
        var carol = await session.PersonAsync("carol", "G-CAR");
        await session.PersonAsync("erin", "G-ERIN", gdDisabled: true);
        gd.AddUser("G-FRANK", "frank");
        gd.AddDepartment(1, "  總公司 ", chief: "G-ADM");
        // 副主管容忍「、」、全形與半形逗號、前後空白與空項；與主管重複的只算一次。
        gd.AddDepartment(2, "業務部", parent: 1, chief: "G-BOB", deputies: " G-CAR 、、g-bob,G-NONE，G-ERIN , G-FRANK");
        gd.AddDepartment(3, "工程部", parent: 1, order: 5);
        gd.AddDepartment(4, "資訊部", parent: 1, order: 1);
        gd.AddDepartment(5, "  ", parent: 1);
        gd.AddDepartment(6, "課務組", parent: 5);
        gd.AddDepartment(7, "測試據點", parent: null);
        gd.AddDepartment(8, "海外分部", parent: 999);

        var (preview, result) = await ImportAsync(session.Admin);

        Assert.Equal(8, preview.SourceUnits);
        Assert.All(preview.Units, x => Assert.Equal(UnitImportAction.Create, x.Action));
        Assert.Equal([1, 2, 3, 4, 7, 8], preview.Units.Select(x => x.GdDepNo).Order());
        Assert.Equal(["總公司", "業務部"], preview.Units.Single(x => x.GdDepNo == 2).Path);
        Assert.Equal(
            [(2, "G-NONE", UnitImportIssueCode.AccountNotFound), (2, "G-ERIN", UnitImportIssueCode.AccountDisabled), (2, "G-FRANK", UnitImportIssueCode.UserNotFound),
             (5, null, UnitImportIssueCode.NameMissing), (6, null, UnitImportIssueCode.ParentSkipped)],
            preview.Issues.Select(x => (x.GdDepNo, x.GdUserId, x.Code)));
        Assert.Equal(
            [(alice, false, true), (bob, false, true), (carol, true, true)],
            preview.Managers.Select(x => (x.UserId, x.Deputy, x.JoinsUnit)).OrderBy(x => x.UserId == alice ? 0 : x.UserId == bob ? 1 : 2));
        Assert.All(preview.Managers, x => Assert.True(x.Added));
        Assert.Equal(new UnitImportResultDto(6, 0, 0, 0, 3, 0, 5), result);

        var units = await ListUnitsAsync(session.Admin);
        var company = units.Single(x => x.GdDepNo == 1);
        Assert.Equal("總公司", company.Name);
        Assert.Equal(["總公司", "測試據點", "海外分部"], units.Where(x => x.ParentId is null).OrderBy(x => x.SortOrder).Select(x => x.Name));
        // 同層依 GD 的順序；沒有順序的排在後面。
        Assert.Equal(["資訊部", "工程部", "業務部"], units.Where(x => x.ParentId == company.Id).OrderBy(x => x.SortOrder).Select(x => x.Name));
        var sales = await MembersAsync(session.Admin, units.Single(x => x.GdDepNo == 2).Id);
        Assert.Equal([(bob, true, false), (carol, true, false)], sales.Select(x => (x.UserId, x.IsManager, x.IsPrimary)).OrderBy(x => x.UserId == bob ? 0 : 1));

        var audit = Assert.Single(await AttendanceApi.AuditAsync(session.Factory, AuditAction));
        Assert.Equal("saved", audit.Result);
        Assert.Contains("\"created\":6", audit.Details);

        // 再匯入一次沒有任何變動，只剩同樣的問題。
        var again = await PreviewImportAsync(session.Admin);
        Assert.Empty(again.Units);
        Assert.Empty(again.Managers);
        Assert.Equal(5, again.Issues.Count);
    }

    [Fact]
    public async Task ReimportingFollowsGdAndDisablesVanishedUnitsWithoutTouchingManualOnes()
    {
        await using var session = await ImportSession.CreateAsync();
        var gd = session.Gd;
        var bob = await session.PersonAsync("bob", "G-BOB");
        var carol = await session.PersonAsync("carol", "G-CAR");
        var dave = await UserIdAsync(session.Factory, "dave");
        gd.AddDepartment(1, "總公司");
        gd.AddDepartment(2, "業務部", parent: 1, chief: "G-BOB");
        gd.AddDepartment(3, "工程部", parent: 1);
        gd.AddDepartment(4, "倉儲部", parent: 1);
        gd.AddDepartment(5, "研發部", parent: 1, chief: "G-CAR");
        await ImportAsync(session.Admin);
        var before = (await ListUnitsAsync(session.Admin)).ToDictionary(x => x.GdDepNo ?? 0);
        var office = await CreateUnitAsync(session.Admin, "專案辦公室", before[1].Id);
        (await SaveMemberAsync(session.Admin, before[5].Id, dave, primary: true)).EnsureSuccessStatusCode();
        // 管理員在本地改了匯入的單位：下次匯入以 GD 為準。
        (await session.Admin.PutAsJsonAsync($"{Units}/{before[3].Id}", new UnitUpdateRequest(before[1].Id, "工程處", true))).EnsureSuccessStatusCode();

        gd.UpdateDepartment(2, "DEP_NAME = N'業務一部', DEP_CHIEF = 'G-CAR'");
        gd.UpdateDepartment(3, "PARENT_NO = 2");
        gd.UpdateDepartment(4, "[Disable] = 1, DEP_CHIEF = 'G-BOB'");
        gd.RemoveDepartment(5);
        var (preview, result) = await ImportAsync(session.Admin);

        Assert.Equal(
            [(2, UnitImportAction.Update, "Name"), (3, UnitImportAction.Update, "Name,Parent,SortOrder"), (4, UnitImportAction.Update, "Enabled,SortOrder"),
             (5, UnitImportAction.Disable, "Enabled")],
            preview.Units.Select(x => (x.GdDepNo, x.Action, string.Join(',', x.Fields))).OrderBy(x => x.GdDepNo));
        Assert.Equal(["總公司", "工程處"], preview.Units.Single(x => x.GdDepNo == 3).PreviousPath);
        Assert.Equal(["總公司", "業務一部", "工程部"], preview.Units.Single(x => x.GdDepNo == 3).Path);
        Assert.Equal([(carol, true, true), (bob, false, false)], preview.Managers.Select(x => (x.UserId, x.Added, x.JoinsUnit)));
        Assert.Equal(new UnitImportResultDto(0, 0, 3, 1, 1, 1, 0), result);

        var after = (await ListUnitsAsync(session.Admin)).ToDictionary(x => x.Id);
        Assert.Equal("業務一部", after[before[2].Id].Name);
        Assert.Equal((before[2].Id, "工程部"), (after[before[3].Id].ParentId, after[before[3].Id].Name));
        Assert.False(after[before[4].Id].Enabled);
        // GD 已沒有的部門停用、不刪除：成員與 GdDepNo 都留著。
        Assert.Equal((false, 5, 2), (after[before[5].Id].Enabled, after[before[5].Id].GdDepNo, after[before[5].Id].MemberCount));
        Assert.Equal((before[1].Id, "專案辦公室", (int?)null, true), (after[office.Id].ParentId, after[office.Id].Name, after[office.Id].GdDepNo, after[office.Id].Enabled));
        // 不再是主管的人保留成員資格；停用的單位不同步主管。
        Assert.Equal([(bob, false), (carol, true)], (await MembersAsync(session.Admin, before[2].Id)).Select(x => (x.UserId, x.IsManager)).OrderBy(x => x.UserId == bob ? 0 : 1));
        Assert.Empty(await MembersAsync(session.Admin, before[4].Id));
    }

    [Fact]
    public async Task ExcludedDepartmentsStayAsTheyAreForThisImportOnly()
    {
        await using var session = await ImportSession.CreateAsync();
        var gd = session.Gd;
        await session.PersonAsync("bob", "G-BOB");
        gd.AddDepartment(1, "總公司");
        gd.AddDepartment(2, "業務部", parent: 1);
        gd.AddDepartment(3, "倉儲部", parent: 1);
        gd.AddDepartment(4, "研發部", parent: 1);
        await ImportAsync(session.Admin);
        var before = (await ListUnitsAsync(session.Admin)).ToDictionary(x => x.GdDepNo ?? 0);
        // 管理員在本地停用業務部，不想被公文系統重新啟用。
        (await session.Admin.PutAsJsonAsync($"{Units}/{before[2].Id}", new UnitUpdateRequest(before[1].Id, "業務部", false))).EnsureSuccessStatusCode();
        gd.UpdateDepartment(2, "DEP_CHIEF = 'G-BOB'");
        gd.RemoveDepartment(3);
        gd.AddDepartment(5, "新事業部", parent: 1);
        gd.AddDepartment(6, "新事業一課", parent: 5);

        var preview = await PreviewImportAsync(session.Admin);
        Assert.Equal([2, 3, 4, 5, 6], preview.Units.Select(x => x.GdDepNo).Order());
        var response = await session.Admin.PostAsJsonAsync(Import, new UnitImportRequest(preview.Fingerprint, [2, 3, 5]));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());

        var after = await ListUnitsAsync(session.Admin);
        Assert.False(after.Single(x => x.Id == before[2].Id).Enabled);
        Assert.True(after.Single(x => x.Id == before[3].Id).Enabled);
        // 排除的新部門不建立，掛在它底下的新部門無處可掛，一併略過。
        Assert.DoesNotContain(after, x => x.GdDepNo is 5 or 6);
        Assert.Empty(await MembersAsync(session.Admin, before[2].Id));
        Assert.Contains(await AttendanceApi.AuditAsync(session.Factory, AuditAction), x => x.Details.Contains("\"excluded\":3"));

        // 排除只限這一次：下次預覽仍列出。
        Assert.Equal([2, 3, 4, 5, 6], (await PreviewImportAsync(session.Admin)).Units.Select(x => x.GdDepNo).Order());
    }

    [Fact]
    public async Task TheFirstImportAdoptsManualUnitsWithTheSameNameUnderTheSameParent()
    {
        await using var session = await ImportSession.CreateAsync();
        var bob = await session.PersonAsync("bob", "G-BOB");
        var company = await CreateUnitAsync(session.Admin, "總公司");
        var sales = await CreateUnitAsync(session.Admin, "業務部", company.Id);
        var other = await CreateUnitAsync(session.Admin, "其他據點");
        (await SaveMemberAsync(session.Admin, sales.Id, bob, primary: true)).EnsureSuccessStatusCode();
        session.Gd.AddDepartment(1, "總公司");
        session.Gd.AddDepartment(2, "業務部", parent: 1, chief: "G-BOB");

        var (preview, result) = await ImportAsync(session.Admin);

        Assert.Equal([(1, company.Id), (2, sales.Id)], preview.Units.Where(x => x.Action == UnitImportAction.Adopt).Select(x => (x.GdDepNo, x.UnitId!.Value)).OrderBy(x => x.GdDepNo));
        Assert.Equal((bob, true, false), preview.Managers.Select(x => (x.UserId, x.Added, x.JoinsUnit)).Single());
        Assert.Equal(new UnitImportResultDto(0, 2, 0, 0, 1, 0, 0), result);
        var units = await ListUnitsAsync(session.Admin);
        Assert.Equal([(company.Id, 1), (sales.Id, 2), (other.Id, (int?)null)], units.Select(x => (x.Id, x.GdDepNo)).OrderBy(x => x.GdDepNo ?? int.MaxValue));
        Assert.Equal((true, true), (await MembersAsync(session.Admin, sales.Id)).Select(x => (x.IsPrimary, x.IsManager)).Single());
    }

    [Fact]
    public async Task DuplicateNamesCyclesAndDepthAreSkippedAndReportedWithoutFailingTheImport()
    {
        await using var session = await ImportSession.CreateAsync();
        var gd = session.Gd;
        gd.AddDepartment(1, "總公司");
        gd.AddDepartment(2, "業務部", parent: 1);
        await ImportAsync(session.Admin);
        var company = (await ListUnitsAsync(session.Admin)).Single(x => x.GdDepNo == 1);
        var manual = await CreateUnitAsync(session.Admin, "工程部", company.Id);

        // 改名撞到手建的同層單位；兩個新部門同名時保留編號小的。
        gd.UpdateDepartment(2, "DEP_NAME = N'工程部'");
        gd.AddDepartment(3, "研發部", parent: 1);
        gd.AddDepartment(4, "研發部", parent: 1);
        // 成環（含指向自己）與掛在環下的部門。
        gd.AddDepartment(10, "甲", parent: 11);
        gd.AddDepartment(11, "乙", parent: 10);
        gd.AddDepartment(12, "丙", parent: 10);
        gd.AddDepartment(13, "丁", parent: 13);
        // 九層：第九層與它的下層都超過深度。
        for (var level = 0; level < 9; level++) gd.AddDepartment(20 + level, $"第{level + 1}層", parent: level == 0 ? 0 : 19 + level);
        gd.AddDepartment(29, "第十層", parent: 28);

        var (preview, result) = await ImportAsync(session.Admin);

        Assert.Equal(
            [(2, UnitImportIssueCode.NameDuplicate), (4, UnitImportIssueCode.NameDuplicate), (10, UnitImportIssueCode.Cycle), (11, UnitImportIssueCode.Cycle),
             (12, UnitImportIssueCode.ParentSkipped), (13, UnitImportIssueCode.Cycle), (28, UnitImportIssueCode.DepthExceeded), (29, UnitImportIssueCode.DepthExceeded)],
            preview.Issues.Select(x => (x.GdDepNo, x.Code)));
        Assert.Equal(9, result.Created);
        var units = await ListUnitsAsync(session.Admin);
        Assert.Equal("業務部", units.Single(x => x.GdDepNo == 2).Name);
        Assert.Equal((manual.Name, (int?)null), units.Where(x => x.Id == manual.Id).Select(x => (x.Name, x.GdDepNo)).Single());
        Assert.Equal([3, 20, 21, 22, 23, 24, 25, 26, 27], units.Where(x => x.GdDepNo is > 2).Select(x => x.GdDepNo!.Value).Order());
    }

    [Fact]
    public async Task AStalePreviewIsRefusedAndAuditedWithoutChangingAnything()
    {
        await using var session = await ImportSession.CreateAsync();
        session.Gd.AddDepartment(1, "總公司");
        var preview = await PreviewImportAsync(session.Admin);

        session.Gd.AddDepartment(2, "業務部", parent: 1);
        await AssertCodeAsync(await ApplyImportAsync(session.Admin, preview.Fingerprint), HttpStatusCode.Conflict, "organization_import_stale");
        Assert.Empty(await ListUnitsAsync(session.Admin));
        Assert.Equal(["organization_import_stale"], (await AttendanceApi.AuditAsync(session.Factory, AuditAction)).Select(x => x.Result));

        // 本地在預覽後有影響結果的變動也一樣。
        preview = await PreviewImportAsync(session.Admin);
        await CreateUnitAsync(session.Admin, "總公司");
        await AssertCodeAsync(await ApplyImportAsync(session.Admin, preview.Fingerprint), HttpStatusCode.Conflict, "organization_import_stale");

        var (fresh, result) = await ImportAsync(session.Admin);
        Assert.Equal([UnitImportAction.Create, UnitImportAction.Adopt], fresh.Units.Select(x => x.Action));
        Assert.Equal(1, result.Created);
    }

    [Fact]
    public async Task AnUnusableGdIsAClearErrorForPreviewAndApply()
    {
        await using (var session = await ImportSession.CreateAsync(configured: false))
        {
            await AssertCodeAsync(await session.Admin.GetAsync(Import), HttpStatusCode.ServiceUnavailable, "organization_import_not_configured");
            await AssertCodeAsync(await ApplyImportAsync(session.Admin, new string('0', 64)), HttpStatusCode.ServiceUnavailable, "organization_import_not_configured");
            Assert.Equal(["organization_import_not_configured"], (await AttendanceApi.AuditAsync(session.Factory, AuditAction)).Select(x => x.Result));
        }
        await using (var session = await ImportSession.CreateAsync(reachable: false))
            await AssertCodeAsync(await session.Admin.GetAsync(Import), HttpStatusCode.ServiceUnavailable, "organization_import_unavailable");
        await using (var session = await ImportSession.CreateAsync())
        {
            // 部門表是空的：照算會停用所有已匯入的單位，所以拒絕。
            await AssertCodeAsync(await session.Admin.GetAsync(Import), HttpStatusCode.Conflict, "organization_import_empty");
            session.Gd.DropDepartments();
            await AssertCodeAsync(await session.Admin.GetAsync(Import), HttpStatusCode.ServiceUnavailable, "organization_import_rejected");
        }
    }

    [Fact]
    public async Task ImportNeedsItsOwnGrant()
    {
        await using var session = await ImportSession.CreateAsync();
        session.Gd.AddDepartment(1, "總公司");
        var viewerId = await UserIdAsync(session.Factory, "viewer");
        await AttendanceApi.GrantAsync(session.Factory, viewerId, "test-organization", FeatureCatalog.Organization);
        using var viewer = await session.Factory.SignedInAsync("viewer");

        // 只有組織頁、沒有匯入動作：預覽也不行。
        Assert.Empty(await ListUnitsAsync(viewer));
        await AssertCodeAsync(await viewer.GetAsync(Import), HttpStatusCode.Forbidden, "feature_forbidden");
        await AssertCodeAsync(await ApplyImportAsync(viewer, new string('0', 64)), HttpStatusCode.Forbidden, "feature_forbidden");
    }

    /// <summary>一個假 GD、一個 host 與登入的管理員 alice；參數模擬 GD 未設定或連不上。</summary>
    private sealed class ImportSession : IAsyncDisposable
    {
        public required FakeGd Gd { get; init; }
        public required AppFactory Factory { get; init; }
        public required HttpClient Admin { get; init; }

        public static async Task<ImportSession> CreateAsync(bool configured = true, bool reachable = true, bool ldap = false)
        {
            var gd = FakeGd.Create();
            var factory = new AppFactory(ldap: ldap, administrators: ["alice"], services: services => services.PostConfigure<DatabasesOptions>(options =>
            {
                if (configured) options[GdDatabase.Name] = reachable ? gd.Databases[GdDatabase.Name] : FakeGd.Missing();
                else options.Remove(GdDatabase.Name);
            }));
            HttpClient admin;
            if (ldap)
            {
                // LDAP 模式下測試標頭不認證，改以 cookie 登入（測試身分要用這條路）。
                admin = factory.CreateClient();
                await Login(admin, "alice");
            }
            else admin = await factory.SignedInAsync();
            return new() { Gd = gd, Factory = factory, Admin = admin };
        }

        /// <summary>建立使用者（登入一次）、設定 AD 帳號，並在 GD 加同帳號的公文帳號 <paramref name="gdId"/>。</summary>
        public async Task<Guid> PersonAsync(string account, string gdId, bool gdDisabled = false)
        {
            var id = await UserIdAsync(Factory, account);
            await CalendarSession.SetAdAccountAsync(Factory, id, account);
            Gd.AddUser(gdId, account, gdDisabled);
            return id;
        }

        public async ValueTask DisposeAsync()
        {
            Admin.Dispose();
            await Factory.DisposeAsync();
            Gd.Dispose();
        }
    }
}
