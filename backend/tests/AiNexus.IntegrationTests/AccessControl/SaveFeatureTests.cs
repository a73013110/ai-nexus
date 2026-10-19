using System.Net;
using System.Net.Http.Json;
using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Platform.Account;
using static AiNexus.IntegrationTests.AccessControl.CreateNavigationGroupTests;
using AiNexus.Features.Admin.AccessControl.Management;
using AiNexus.Features.Admin.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.AccessControl;

public sealed class SaveFeatureTests
{
    [Fact]
    public async Task AdministratorMovesAFeatureToAnotherGroupWhileRouteAndIconComeFromTheCatalog()
    {
        await using var factory = new AppFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        (await admin.PutAsJsonAsync("/api/v1/admin/features/jobs", new FeatureUpdateRequest("背景任務", 70, true, "work"))).EnsureSuccessStatusCode();
        var feature = Assert.Single((await admin.GetFromJsonAsync<AdminCatalogDto>("/api/v1/admin/catalog"))!.Features, x => x.Id == FeatureCatalog.Jobs);
        Assert.Equal(("work", "tasks", 70, "/work/jobs"), (feature.GroupId, feature.Icon, feature.SortOrder, feature.Route));
        Assert.Contains((await admin.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Access.Features, x => x.Id == FeatureCatalog.Jobs && x.GroupId == "work" && x.Route == "/work/jobs");
        (await admin.PutAsJsonAsync("/api/v1/admin/features/jobs", new FeatureUpdateRequest("背景任務", 70, true, null))).EnsureSuccessStatusCode();
        Assert.Null(Assert.Single((await admin.GetFromJsonAsync<AdminCatalogDto>("/api/v1/admin/catalog"))!.Features, x => x.Id == FeatureCatalog.Jobs).GroupId);
    }

    [Fact]
    public async Task NamesEqualToTheCatalogDefaultOrBlankAreStoredAsNoOverride()
    {
        await using var factory = new AppFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        (await admin.PutAsJsonAsync("/api/v1/admin/features/jobs", new FeatureUpdateRequest("排程工作", 55, true, "collaboration"))).EnsureSuccessStatusCode();
        Assert.Equal("排程工作", await StoredNameAsync(factory));
        Assert.Contains((await admin.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Access.Features, x => x.Id == FeatureCatalog.Jobs && x.Name == "排程工作");

        (await admin.PutAsJsonAsync("/api/v1/admin/features/jobs", new FeatureUpdateRequest(" 背景任務 ", 55, true, "collaboration"))).EnsureSuccessStatusCode();
        Assert.Null(await StoredNameAsync(factory));
        (await admin.PutAsJsonAsync("/api/v1/admin/features/jobs", new FeatureUpdateRequest("排程工作", 55, true, "collaboration"))).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync("/api/v1/admin/features/jobs", new FeatureUpdateRequest("  ", 55, true, "collaboration"))).EnsureSuccessStatusCode();
        Assert.Null(await StoredNameAsync(factory));
        Assert.Contains((await admin.GetFromJsonAsync<AdminCatalogDto>("/api/v1/admin/catalog"))!.Features, x => x.Id == FeatureCatalog.Jobs && x.Name == "背景任務");
    }

    [Fact]
    public async Task UnknownGroupsAreRejected()
    {
        await using var factory = new AppFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        await AssertCodeAsync(await admin.PutAsJsonAsync("/api/v1/admin/features/jobs", new FeatureUpdateRequest("背景任務", 55, true, "missing")), HttpStatusCode.NotFound, "navigation_group_not_found");
        var feature = Assert.Single((await admin.GetFromJsonAsync<AdminCatalogDto>("/api/v1/admin/catalog"))!.Features, x => x.Id == FeatureCatalog.Jobs);
        Assert.Equal("collaboration", feature.GroupId);
    }

    private static async Task<string?> StoredNameAsync(AppFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<Feature>().Where(x => x.Id == FeatureCatalog.Jobs).Select(x => x.Name).SingleAsync();
    }
}
