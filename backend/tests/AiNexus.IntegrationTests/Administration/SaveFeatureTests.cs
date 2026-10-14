using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Account;
using AiNexus.Features.Administration.Roles;
using static AiNexus.IntegrationTests.Administration.CreateNavigationGroupTests;

namespace AiNexus.IntegrationTests.Administration;

public sealed class SaveFeatureTests
{
    [Fact]
    public async Task AdministratorMovesAFeatureToAnotherGroupWithANewIcon()
    {
        await using var factory = new NexusFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        (await admin.PutAsJsonAsync("/api/v1/admin/features/tasks", new FeatureUpdateRequest("背景任務", 70, true, "work", "workspace"))).EnsureSuccessStatusCode();
        var feature = Assert.Single((await admin.GetFromJsonAsync<AdminCatalogDto>("/api/v1/admin/catalog"))!.Features, x => x.Id == "tasks");
        Assert.Equal(("work", "workspace", 70), (feature.GroupId, feature.Icon, feature.SortOrder));
        Assert.Contains((await admin.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Access.Features, x => x.Id == "tasks" && x.GroupId == "work" && x.Icon == "workspace");
        (await admin.PutAsJsonAsync("/api/v1/admin/features/tasks", new FeatureUpdateRequest("背景任務", 70, true, null, "tasks"))).EnsureSuccessStatusCode();
        Assert.Null(Assert.Single((await admin.GetFromJsonAsync<AdminCatalogDto>("/api/v1/admin/catalog"))!.Features, x => x.Id == "tasks").GroupId);
    }

    [Fact]
    public async Task UnknownGroupsAndMalformedIconsAreRejected()
    {
        await using var factory = new NexusFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        await AssertCodeAsync(await admin.PutAsJsonAsync("/api/v1/admin/features/tasks", new FeatureUpdateRequest("背景任務", 55, true, "missing", "tasks")), HttpStatusCode.NotFound, "navigation_group_not_found");
        await AssertCodeAsync(await admin.PutAsJsonAsync("/api/v1/admin/features/tasks", new FeatureUpdateRequest("背景任務", 55, true, "collaboration", "<svg>")), HttpStatusCode.BadRequest, "invalid_feature_icon");
        var feature = Assert.Single((await admin.GetFromJsonAsync<AdminCatalogDto>("/api/v1/admin/catalog"))!.Features, x => x.Id == "tasks");
        Assert.Equal(("collaboration", "tasks"), (feature.GroupId, feature.Icon));
    }
}
