using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.AccessControl.Management;
using AiNexus.Features.Admin.AccessControl.Navigation;
using AiNexus.Features.Admin.Identity.Management;
using AiNexus.Features.Admin.Operations;
using AiNexus.Features.Admin.Organization;
using static AiNexus.IntegrationTests.Support.IdentityApi;
using static AiNexus.IntegrationTests.Support.NavigationApi;
using static AiNexus.IntegrationTests.Support.OrganizationApi;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.AccessControl;

/// <summary>看舊畫面存檔要被拒絕，內容不變；各表的版本都由這裡守住。</summary>
public sealed class AdministrativeVersionTests
{
    private static Task AssertStaleAsync(HttpResponseMessage response)
        => AssertCodeAsync(response, HttpStatusCode.Conflict, "admin_write_conflict");

    [Fact]
    public async Task AFeatureSavedFromAnOldScreenIsRejected()
    {
        await using var factory = new AppFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        const string Url = "/api/v1/admin/features/jobs";
        var seen = await factory.FeatureVersionAsync("jobs");

        (await admin.PutAsJsonAsync(Url, new FeatureUpdateRequest("先存的", true, seen))).EnsureSuccessStatusCode();
        await AssertStaleAsync(await admin.PutAsJsonAsync(Url, new FeatureUpdateRequest("後存的", true, seen)));

        Assert.Contains((await admin.GetFromJsonAsync<AdminCatalogDto>("/api/v1/admin/catalog"))!.Features, x => x.Id == "jobs" && x.Name == "先存的");
    }

    // 角色與群組是 upsert：不帶版本是新增，已經有人建立就不能蓋掉；帶版本是修改，列不在了也不能變成新增。
    [Fact]
    public async Task ARoleIsCreatedWithoutAVersionAndChangedOnlyWithTheCurrentOne()
    {
        await using var factory = new AppFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        const string Url = "/api/v1/admin/roles/analyst";

        (await admin.PutAsJsonAsync(Url, new RoleUpdateRequest("分析人員", true, ["workspace"]))).EnsureSuccessStatusCode();
        await AssertStaleAsync(await admin.PutAsJsonAsync(Url, new RoleUpdateRequest("同時新增", true, ["workspace"])));

        // 只改子表也算改了這一列，否則另一位管理員會蓋掉這次的群組。
        var seen = (await factory.RoleVersionAsync("analyst"))!.Value;
        (await admin.PutAsJsonAsync(Url, new RoleUpdateRequest("分析人員", true, [BuiltInAccess.AdministratorsGroup], seen))).EnsureSuccessStatusCode();
        Assert.NotEqual(seen, await factory.RoleVersionAsync("analyst"));
        await AssertStaleAsync(await admin.PutAsJsonAsync(Url, new RoleUpdateRequest("分析人員", true, ["workspace"], seen)));

        await AssertStaleAsync(await admin.PutAsJsonAsync("/api/v1/admin/roles/gone", new RoleUpdateRequest("已刪除", true, ["workspace"], seen)));
    }

    [Fact]
    public async Task AGroupIsCreatedWithoutAVersionAndChangedOnlyWithTheCurrentOne()
    {
        await using var factory = new AppFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        const string Url = "/api/v1/admin/groups/analysts";

        (await admin.PutAsJsonAsync(Url, new GroupUpdateRequest("分析", true, ["chat"]))).EnsureSuccessStatusCode();
        await AssertStaleAsync(await admin.PutAsJsonAsync(Url, new GroupUpdateRequest("同時新增", true, ["chat"])));

        var seen = (await factory.GroupVersionAsync("analysts"))!.Value;
        (await admin.PutAsJsonAsync(Url, new GroupUpdateRequest("分析", true, ["jobs"], RowVersion: seen))).EnsureSuccessStatusCode();
        Assert.NotEqual(seen, await factory.GroupVersionAsync("analysts"));
        await AssertStaleAsync(await admin.PutAsJsonAsync(Url, new GroupUpdateRequest("分析", true, ["chat"], RowVersion: seen)));
    }

    [Fact]
    public async Task ANavigationItemChangedFromAnOldScreenIsRejectedForUpdateAndDelete()
    {
        await using var factory = new AppFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        var group = await CreateAsync(admin, Group("報表"));
        NavigationItemUpdateRequest Named(string name, long version) => new("group", null, name, null, true, null, null, version);

        (await admin.PutAsJsonAsync($"{Items}/{group.Id}", Named("先存的", group.RowVersion))).EnsureSuccessStatusCode();
        await AssertStaleAsync(await admin.PutAsJsonAsync($"{Items}/{group.Id}", Named("後存的", group.RowVersion)));
        await AssertStaleAsync(await admin.DeleteAsync($"{Items}/{group.Id}?rowVersion={group.RowVersion}"));

        Assert.Contains(await ListAsync(admin), x => x.Id == group.Id && x.Name == "先存的");
    }

    // EF 不會為內容沒變的實體送 UPDATE：若不強制，同內容的存檔既不比對也不升版，舊畫面就抓不到。
    [Fact]
    public async Task AUnitSavedWithUnchangedContentStillChecksAndRaisesTheVersion()
    {
        await using var factory = new AppFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        var unit = await CreateUnitAsync(admin, "資訊處");
        UnitUpdateRequest Same(long version) => new(null, "資訊處", true, null, version);

        (await admin.PutAsJsonAsync($"{Units}/{unit.Id}", Same(unit.RowVersion))).EnsureSuccessStatusCode();
        var raised = await factory.UnitVersionAsync(unit.Id);
        Assert.NotEqual(unit.RowVersion, raised);

        await AssertStaleAsync(await admin.PutAsJsonAsync($"{Units}/{unit.Id}", Same(unit.RowVersion)));
        await AssertStaleAsync(await admin.DeleteAsync($"{Units}/{unit.Id}?rowVersion={unit.RowVersion}"));
        Assert.Equal(raised, await factory.UnitVersionAsync(unit.Id));
        (await admin.DeleteAsync($"{Units}/{unit.Id}?rowVersion={raised}")).EnsureSuccessStatusCode();
    }

    // 每個管理端的帳號寫入都比對並加一；Users 有登入與活動欄位會常改，所以不用 rowversion。
    [Fact]
    public async Task EveryAccountWriteRaisesTheProfileVersionByOneAndRefusesAnOldOne()
    {
        await using var factory = new AppFactory(ldap: true, administrators: ["alice"]);
        using var admin = factory.CreateClient(); await Login(admin, "alice");
        using var bob = factory.CreateClient(); var bobId = (await Login(bob, "bob")).Id;
        var local = await CreateUser(admin, LocalAccount("tester"));

        async Task RaisesAsync(Guid id, Func<int, Task<HttpResponseMessage>> write)
        {
            var seen = await factory.ProfileVersionAsync(id);
            (await write(seen)).EnsureSuccessStatusCode();
            Assert.Equal(seen + 1, await factory.ProfileVersionAsync(id));
            await AssertStaleAsync(await write(seen));
            Assert.Equal(seen + 1, await factory.ProfileVersionAsync(id));
        }

        await RaisesAsync(local, version => admin.PutAsJsonAsync($"/api/v1/admin/users/{local}", LocalAccount("tester") with { DisplayName = "測試帳號", ProfileVersion = version }));
        await RaisesAsync(local, version => admin.PutAsJsonAsync($"/api/v1/admin/users/{local}/email", new UserEmailRequest("tester@fixture.test", version)));
        await RaisesAsync(local, version => admin.PutAsJsonAsync($"/api/v1/admin/users/{local}/roles", new UserRolesRequest(["member"], version)));
        await RaisesAsync(bobId, version => admin.PutAsJsonAsync($"/api/v1/admin/users/{bobId}/ad-binding", new DirectoryBindingRequest("erin.chen", version)));

        var bound = await factory.ProfileVersionAsync(bobId);
        await AssertStaleAsync(await admin.DeleteAsync($"/api/v1/admin/users/{bobId}/ad-binding?profileVersion={bound - 1}"));
        (await admin.DeleteAsync($"/api/v1/admin/users/{bobId}/ad-binding?profileVersion={bound}")).EnsureSuccessStatusCode();
        Assert.Equal(bound + 1, await factory.ProfileVersionAsync(bobId));

        var current = await factory.ProfileVersionAsync(local);
        await AssertStaleAsync(await admin.DeleteAsync($"/api/v1/admin/users/{local}?profileVersion={current - 1}"));
        (await admin.DeleteAsync($"/api/v1/admin/users/{local}?profileVersion={current}")).EnsureSuccessStatusCode();
    }

    // 登入與活動會一直發生；若也升版，管理員開著畫面就存不了檔。
    [Fact]
    public async Task SigningInWithNothingChangedKeepsTheProfileVersion()
    {
        await using var factory = new AppFactory(ldap: true, administrators: ["alice"]);
        using var first = factory.CreateClient(); var bob = (await Login(first, "bob")).Id;
        var seen = await factory.ProfileVersionAsync(bob);

        using var again = factory.CreateClient(); await Login(again, "bob");
        (await again.GetAsync("/api/v1/me")).EnsureSuccessStatusCode();

        Assert.Equal(seen, await factory.ProfileVersionAsync(bob));
    }

    // 同層名稱不可重複跨多列：宣告的鎖讓它們排隊，後面的讀得到前面的結果，回業務錯誤而不是撞索引。
    [Fact]
    public async Task UnitsCreatedAtOnceWithTheSameNameLeaveOneAndTellTheRestWhy()
    {
        await using var factory = new AppFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => admin.PostAsJsonAsync(Units, new UnitCreateRequest(null, "同名單位", true))));

        Assert.Single(responses, x => x.IsSuccessStatusCode);
        foreach (var refused in responses.Where(x => !x.IsSuccessStatusCode))
            await AssertCodeAsync(refused, HttpStatusCode.Conflict, "organization_unit_name_duplicate");
        Assert.Single(await ListUnitsAsync(admin), x => x.Name == "同名單位");
    }

    [Fact]
    public async Task APageAddedToTheMenuAtOnceIsKeptOnlyOnce()
    {
        await using var factory = new AppFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        (await admin.DeleteAsync($"{Items}/work.chat?rowVersion={await factory.NavigationVersionAsync("work.chat")}")).EnsureSuccessStatusCode();

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => admin.PostAsJsonAsync(Items, Page("chat", BuiltInAccess.WorkNavigation))));

        Assert.Single(responses, x => x.IsSuccessStatusCode);
        foreach (var refused in responses.Where(x => !x.IsSuccessStatusCode))
            await AssertCodeAsync(refused, HttpStatusCode.Conflict, "navigation_page_duplicate");
        Assert.Single(await ListAsync(admin), x => x.FeatureId == "chat");
    }
}
