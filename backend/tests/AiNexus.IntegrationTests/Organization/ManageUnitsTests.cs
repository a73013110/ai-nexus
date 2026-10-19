using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Admin.Audit;
using AiNexus.Features.Admin.Organization;
using static AiNexus.IntegrationTests.Support.OrganizationApi;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.Organization;

public sealed class ManageUnitsTests
{
    [Fact]
    public async Task UnitsFormATreeOrderedWithinEachLevel()
    {
        await using var factory = new AppFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        var company = await CreateUnitAsync(admin, "  總公司  ");
        var it = await CreateUnitAsync(admin, "資訊處", company.Id);
        var hr = await CreateUnitAsync(admin, "人資處", company.Id, enabled: false);

        Assert.Equal("總公司", company.Name);
        Assert.Equal([10, 20], new[] { it.SortOrder, hr.SortOrder });
        var units = await ListUnitsAsync(admin);
        Assert.Equal([company.Id], units.Where(x => x.ParentId is null).Select(x => x.Id));
        Assert.Equal([it.Id, hr.Id], units.Where(x => x.ParentId == company.Id).Select(x => x.Id));
        Assert.False(units.Single(x => x.Id == hr.Id).Enabled);

        (await admin.PutAsJsonAsync($"{Units}/order", new UnitOrderRequest(company.Id, [hr.Id, it.Id]))).EnsureSuccessStatusCode();
        Assert.Equal([hr.Id, it.Id], (await ListUnitsAsync(admin)).Where(x => x.ParentId == company.Id).Select(x => x.Id));
        // 清單少了一個（畫面過期）或多了別層的單位都拒絕，不會只排一部分。
        await AssertCodeAsync(await admin.PutAsJsonAsync($"{Units}/order", new UnitOrderRequest(company.Id, [it.Id])), HttpStatusCode.Conflict, "organization_order_stale");
        await AssertCodeAsync(await admin.PutAsJsonAsync($"{Units}/order", new UnitOrderRequest(company.Id, [it.Id, hr.Id, company.Id])), HttpStatusCode.Conflict, "organization_order_stale");
        Assert.Single((await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=admin.organization_order&result=saved"))!);
    }

    [Fact]
    public async Task MovingKeepsTheSubtreeAndRejectsCyclesDepthAndDuplicateNames()
    {
        await using var factory = new AppFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        var company = await CreateUnitAsync(admin, "總公司");
        var it = await CreateUnitAsync(admin, "資訊處", company.Id);
        var systems = await CreateUnitAsync(admin, "系統部", it.Id);
        var other = await CreateUnitAsync(admin, "子公司");

        (await admin.PutAsJsonAsync($"{Units}/{it.Id}", new UnitUpdateRequest(other.Id, "資訊處", true))).EnsureSuccessStatusCode();
        var units = await ListUnitsAsync(admin);
        Assert.Equal(other.Id, units.Single(x => x.Id == it.Id).ParentId);
        Assert.Equal(it.Id, units.Single(x => x.Id == systems.Id).ParentId);

        await AssertCodeAsync(await admin.PutAsJsonAsync($"{Units}/{other.Id}", new UnitUpdateRequest(systems.Id, "子公司", true)), HttpStatusCode.BadRequest, "organization_unit_cycle");
        await AssertCodeAsync(await admin.PutAsJsonAsync($"{Units}/{it.Id}", new UnitUpdateRequest(it.Id, "資訊處", true)), HttpStatusCode.BadRequest, "organization_unit_cycle");
        await AssertCodeAsync(await admin.PutAsJsonAsync($"{Units}/{it.Id}", new UnitUpdateRequest(Guid.NewGuid(), "資訊處", true)), HttpStatusCode.BadRequest, "organization_parent_invalid");
        await AssertCodeAsync(await admin.PostAsJsonAsync(Units, new UnitCreateRequest(other.Id, "資訊處", true)), HttpStatusCode.Conflict, "organization_unit_name_duplicate");
        await AssertCodeAsync(await admin.PutAsJsonAsync($"{Units}/{Guid.NewGuid()}", new UnitUpdateRequest(null, "不存在", true)), HttpStatusCode.NotFound, "organization_unit_not_found");

        var parent = systems.Id;
        // 上限 8 層（UnitRules.MaxDepth）；目前已有 3 層。
        for (var depth = 4; depth <= 8; depth++) parent = (await CreateUnitAsync(admin, $"第{depth}層", parent)).Id;
        await AssertCodeAsync(await admin.PostAsJsonAsync(Units, new UnitCreateRequest(parent, "太深", true)), HttpStatusCode.BadRequest, "organization_depth_exceeded");
        // 被拒絕的變更也留下錯誤代碼的稽核。
        Assert.NotEmpty((await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=admin.organization_unit&result=organization_unit_cycle"))!);
    }

    [Fact]
    public async Task UnitsMustBeEmptiedBeforeDeletion()
    {
        await using var factory = new AppFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        var bob = await UserIdAsync(factory, "bob");
        var company = await CreateUnitAsync(admin, "總公司");
        var it = await CreateUnitAsync(admin, "資訊處", company.Id);
        (await SaveMemberAsync(admin, it.Id, bob)).EnsureSuccessStatusCode();

        await AssertCodeAsync(await admin.DeleteAsync($"{Units}/{company.Id}"), HttpStatusCode.Conflict, "organization_unit_not_empty");
        await AssertCodeAsync(await admin.DeleteAsync($"{Units}/{it.Id}"), HttpStatusCode.Conflict, "organization_unit_not_empty");
        (await admin.DeleteAsync($"{Units}/{it.Id}/members/{bob}")).EnsureSuccessStatusCode();
        (await admin.DeleteAsync($"{Units}/{it.Id}")).EnsureSuccessStatusCode();
        (await admin.DeleteAsync($"{Units}/{company.Id}")).EnsureSuccessStatusCode();
        Assert.Empty(await ListUnitsAsync(admin));
        await AssertCodeAsync(await admin.DeleteAsync($"{Units}/{company.Id}"), HttpStatusCode.NotFound, "organization_unit_not_found");
        Assert.Equal(2, (await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=admin.organization_unit_delete&result=saved"))!.Length);
    }

    [Fact]
    public async Task NamesAreValidatedAndMembersCannotManageUnits()
    {
        await using var factory = new AppFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        await AssertCodeAsync(await admin.PostAsJsonAsync(Units, new UnitCreateRequest(null, "   ", true)), HttpStatusCode.BadRequest, "validation_failed");
        await AssertCodeAsync(await admin.PostAsJsonAsync(Units, new UnitCreateRequest(null, new string('部', 121), true)), HttpStatusCode.BadRequest, "validation_failed");

        using var member = await factory.SignedInAsync("bob");
        await AssertCodeAsync(await member.GetAsync(Units), HttpStatusCode.Forbidden, "feature_forbidden");
        await AssertCodeAsync(await member.PostAsJsonAsync(Units, new UnitCreateRequest(null, "總公司", true)), HttpStatusCode.Forbidden, "feature_forbidden");
    }
}
