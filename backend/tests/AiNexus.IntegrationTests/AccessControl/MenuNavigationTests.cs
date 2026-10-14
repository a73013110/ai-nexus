using System.Net.Http.Json;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Account;
using AiNexus.Features.Administration.Navigation;
using AiNexus.Features.Administration.Roles;

namespace AiNexus.IntegrationTests.AccessControl;

public sealed class MenuNavigationTests
{
    [Fact]
    public async Task MembersReceiveOnlyTheGroupsTheirFeaturesNeed()
    {
        await using var factory = new NexusFactory(); using var member = await factory.SignedInAsync();
        var access = (await member.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Access;
        // quality is the only system feature members hold, so the system group comes with it.
        Assert.Equal(["work", "collaboration", "system"], access.NavigationGroups.Select(x => x.Id));
        Assert.Contains(access.Features, x => x.Id == "dashboard" && x.GroupId == null && x.Icon == "dashboard");
        Assert.All(access.NavigationGroups, x => Assert.Null(x.ParentId));
    }

    [Fact]
    public async Task GroupsHoldingOnlyGrantOnlyFeaturesOrNothingAreHiddenAndAncestorsAreKept()
    {
        await using var factory = new NexusFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        (await admin.PostAsJsonAsync("/api/v1/admin/navigation-groups", new NavigationGroupCreateRequest("diagnosis", "診斷", "system", 50))).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync("/api/v1/admin/navigation-groups", new NavigationGroupCreateRequest("empty", "空分群", null, 40))).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync("/api/v1/admin/navigation-groups", new NavigationGroupCreateRequest("permissions", "權限", null, 50))).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync("/api/v1/admin/features/logs.export", new FeatureUpdateRequest("日誌匯出", 96, true, "permissions", "download"))).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync("/api/v1/admin/features/monitoring", new FeatureUpdateRequest("即時監控", 93, true, "diagnosis", "activity"))).EnsureSuccessStatusCode();

        var access = (await admin.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Access;
        var groups = access.NavigationGroups.Select(x => x.Id).ToArray();
        Assert.Contains("diagnosis", groups);
        Assert.Contains("system", groups);
        Assert.DoesNotContain("empty", groups);
        Assert.DoesNotContain("permissions", groups);
        // The grant-only feature is still granted; only the menu leaves it out.
        Assert.Contains(access.Features, x => x.Id == "logs.export" && x.Route == "");

        // A group whose only feature is disabled disappears with it.
        (await admin.PutAsJsonAsync("/api/v1/admin/features/monitoring", new FeatureUpdateRequest("即時監控", 93, false, "diagnosis", "activity"))).EnsureSuccessStatusCode();
        Assert.DoesNotContain((await admin.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Access.NavigationGroups, x => x.Id == "diagnosis");
    }

    [Fact]
    public async Task AccessPreviewCarriesTheSameMenuGroups()
    {
        await using var factory = new NexusFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        using var bob = await factory.SignedInAsync("bob");
        var me = (await bob.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        var preview = (await admin.GetFromJsonAsync<AccessDto>($"/api/v1/admin/users/{me.Id}/access"))!;
        Assert.Equal(me.Access.NavigationGroups, preview.NavigationGroups);
        Assert.NotEmpty(preview.NavigationGroups);
    }
}
