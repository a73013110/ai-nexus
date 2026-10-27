using System.Net;
using System.Net.Http.Json;
using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.AccessControl.Management;
using AiNexus.Features.Admin.Audit;
using AiNexus.Features.Admin.Operations;
using AiNexus.Features.Platform.Account;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.AccessControl;

public sealed class SaveFeatureTests
{
    [Fact]
    public async Task CatalogListsFeaturesInCatalogOrderWithRouteAndIconFromTheCatalog()
    {
        await using var factory = new AppFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        var features = (await admin.GetFromJsonAsync<AdminCatalogDto>("/api/v1/admin/catalog"))!.Features;
        Assert.Equal(FeatureCatalog.Grants.Select(x => x.Id), features.Select(x => x.Id));
        var jobs = Assert.Single(features, x => x.Id == FeatureCatalog.Jobs);
        Assert.Equal(("tasks", "/work/jobs", true), (jobs.Icon, jobs.Route, jobs.Enabled));
    }

    [Fact]
    public async Task NamesEqualToTheCatalogDefaultOrBlankAreStoredAsNoOverride()
    {
        await using var factory = new AppFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        (await admin.PutAsJsonAsync("/api/v1/admin/features/jobs", new FeatureUpdateRequest("排程工作", true))).EnsureSuccessStatusCode();
        Assert.Equal("排程工作", await StoredNameAsync(factory));
        Assert.Contains((await admin.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Access.Features, x => x.Id == FeatureCatalog.Jobs && x.Name == "排程工作");

        (await admin.PutAsJsonAsync("/api/v1/admin/features/jobs", new FeatureUpdateRequest(" 背景任務 ", true))).EnsureSuccessStatusCode();
        Assert.Null(await StoredNameAsync(factory));
        (await admin.PutAsJsonAsync("/api/v1/admin/features/jobs", new FeatureUpdateRequest("排程工作", true))).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync("/api/v1/admin/features/jobs", new FeatureUpdateRequest("  ", true))).EnsureSuccessStatusCode();
        Assert.Null(await StoredNameAsync(factory));
        Assert.Contains((await admin.GetFromJsonAsync<AdminCatalogDto>("/api/v1/admin/catalog"))!.Features, x => x.Id == FeatureCatalog.Jobs && x.Name == "背景任務");
        Assert.Equal(4, (await admin.GetFromJsonAsync<AuditDto[]>("/api/v1/admin/audit?action=access-control.feature.updated&result=success"))!.Length);
    }

    [Fact]
    public async Task DisablingAFeatureHidesItsMenuEntryAndUnknownFeaturesAreNotFound()
    {
        await using var factory = new AppFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        (await admin.PutAsJsonAsync("/api/v1/admin/features/jobs", new FeatureUpdateRequest(null, false))).EnsureSuccessStatusCode();
        Assert.False(Assert.Single((await admin.GetFromJsonAsync<AdminCatalogDto>("/api/v1/admin/catalog"))!.Features, x => x.Id == FeatureCatalog.Jobs).Enabled);
        var collaboration = Assert.Single((await admin.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Access.Navigation, x => x.Id == BuiltInAccess.CollaborationNavigation);
        Assert.DoesNotContain(collaboration.Children, x => x.FeatureId == FeatureCatalog.Jobs);
        await AssertCodeAsync(await admin.PutAsJsonAsync("/api/v1/admin/features/missing", new FeatureUpdateRequest(null, true)), HttpStatusCode.NotFound, "admin_resource_not_found");
    }

    private static async Task<string?> StoredNameAsync(AppFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<Feature>().Where(x => x.Id == FeatureCatalog.Jobs).Select(x => x.Name).SingleAsync();
    }
}
