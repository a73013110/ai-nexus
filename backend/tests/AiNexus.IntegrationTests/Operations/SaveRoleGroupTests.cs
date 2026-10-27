using System.Net;
using System.Net.Http.Json;
using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Audit;
using AiNexus.Features.Admin.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.Operations;

public sealed class SaveRoleGroupTests
{
    [Fact]
    public async Task GrantsCarryExactlyOneDeclaredScopeAndOtherFeaturesNone()
    {
        await using var factory = new AppFactory(administrators: ["alice"]);
        using var admin = await factory.SignedInAsync();
        const string Group = "/api/v1/admin/groups/reviewers";
        Task<HttpResponseMessage> Save(string[] features, Dictionary<string, string>? scopes) => admin.PutAsJsonAsync(Group, new GroupUpdateRequest("稽核查閱", true, features, Scopes: scopes));

        // 有宣告範圍的功能一定要選一個它支援的；沒有宣告的不能帶；範圍只能給這次授予的功能。
        await AssertCodeAsync(await Save(["audit"], null), HttpStatusCode.BadRequest, "invalid_data_scope");
        await AssertCodeAsync(await Save(["audit"], new() { ["audit"] = "assigned" }), HttpStatusCode.BadRequest, "invalid_data_scope");
        await AssertCodeAsync(await Save(["audit", "chat"], new() { ["audit"] = "all", ["chat"] = "own" }), HttpStatusCode.BadRequest, "invalid_data_scope");
        await AssertCodeAsync(await Save(["chat"], new() { ["audit"] = "all" }), HttpStatusCode.BadRequest, "invalid_data_scope");

        (await Save(["audit", "chat"], new() { ["audit"] = "unit-tree" })).EnsureSuccessStatusCode();
        var group = (await admin.GetFromJsonAsync<AdminCatalogDto>("/api/v1/admin/catalog"))!.Groups.Single(x => x.Id == "reviewers");
        Assert.Equal(new Dictionary<string, string> { ["audit"] = "unit-tree" }, group.Scopes);
        (await Save(["audit", "chat"], new() { ["audit"] = "own" })).EnsureSuccessStatusCode();

        using (var scope = factory.Services.CreateScope())
        {
            var grants = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<RoleGroupFeature>().Where(x => x.GroupId == "reviewers").OrderBy(x => x.FeatureId).ToListAsync();
            Assert.Equal([("audit", (string?)"own"), ("chat", null)], grants.Select(x => (x.FeatureId, x.Scope)));
        }
        var builtIn = (await admin.GetFromJsonAsync<AdminCatalogDto>("/api/v1/admin/catalog"))!.Groups.Single(x => x.Id == BuiltInAccess.AdministratorsGroup);
        Assert.Equal("all", builtIn.Scopes[FeatureCatalog.Audit]);

        // 拒絕與變更都留下稽核，快照看得出範圍的前後。
        var audit = (await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=access-control.group.updated"))!;
        Assert.Contains(audit, x => x.Result == "invalid_data_scope");
        Assert.Contains("""{"featureId":"audit","scope":"unit-tree","managerScope":null}""", audit.First(x => x.Result == "completed").DetailsJson);
    }

    [Fact]
    public async Task ManagerScopesAreOptionalAndOnlyWhereTheCatalogDeclaresThem()
    {
        await using var factory = new AppFactory(administrators: ["alice"]);
        using var admin = await factory.SignedInAsync();
        const string Group = "/api/v1/admin/groups/leads";
        const string Records = FeatureCatalog.AttendanceRecords;
        Task<HttpResponseMessage> Save(string[] features, Dictionary<string, string> scopes, Dictionary<string, string>? managers) =>
            admin.PutAsJsonAsync(Group, new GroupUpdateRequest("單位主管", true, features, Scopes: scopes, ManagerScopes: managers));

        // 沒宣告主管範圍的功能不能帶；帶了就要是宣告支援的值；只能給這次授予的功能。
        await AssertCodeAsync(await Save(["audit"], new() { ["audit"] = "own" }, new() { ["audit"] = "managed" }), HttpStatusCode.BadRequest, "invalid_data_scope");
        await AssertCodeAsync(await Save([Records], new() { [Records] = "own" }, new() { [Records] = "unit-tree" }), HttpStatusCode.BadRequest, "invalid_data_scope");
        await AssertCodeAsync(await Save(["chat"], [], new() { [Records] = "managed" }), HttpStatusCode.BadRequest, "invalid_data_scope");

        // 不帶＝不另外；帶了就存在這筆授權上，目錄回應看得到。
        (await Save([Records], new() { [Records] = "own" }, null)).EnsureSuccessStatusCode();
        Assert.Empty((await admin.GetFromJsonAsync<AdminCatalogDto>("/api/v1/admin/catalog"))!.Groups.Single(x => x.Id == "leads").ManagerScopes);
        (await Save([Records], new() { [Records] = "own" }, new() { [Records] = "managed" })).EnsureSuccessStatusCode();
        var catalog = (await admin.GetFromJsonAsync<AdminCatalogDto>("/api/v1/admin/catalog"))!;
        Assert.Equal(new Dictionary<string, string> { [Records] = "managed" }, catalog.Groups.Single(x => x.Id == "leads").ManagerScopes);
        Assert.Equal("managed-tree", catalog.Groups.Single(x => x.Id == BuiltInAccess.WorkspaceGroup).ManagerScopes[Records]);
        Assert.Empty(catalog.Groups.Single(x => x.Id == BuiltInAccess.AdministratorsGroup).ManagerScopes);

        // 稽核快照包含主管範圍的前後。
        var audit = (await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=access-control.group.updated"))!;
        Assert.Contains(audit, x => x.Result == "invalid_data_scope");
        Assert.Contains($$"""{"featureId":"{{Records}}","scope":"own","managerScope":"managed"}""", audit.First(x => x.Result == "completed").DetailsJson);
    }
}
