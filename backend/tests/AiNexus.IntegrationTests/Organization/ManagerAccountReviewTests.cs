using AiNexus.Data.App;
using AiNexus.Data.App.Organization;
using AiNexus.Features.Admin.AccessControl;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.IdentityApi;
using static AiNexus.IntegrationTests.Support.OrganizationApi;

namespace AiNexus.IntegrationTests.Organization;

public sealed class ManagerAccountReviewTests
{
    [Fact]
    public async Task DisabledManagersAreListedOnlyWhileTheirActiveUnitHasNoOtherActiveManager()
    {
        await using var factory = new AppFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        // 測試身分不在 AD 夾具裡，讓目錄查不到以免混入「AD 找不到」的待辦。
        factory.AdDirectory.Unavailable = true;
        var bob = await UserIdAsync(factory, "bob");
        var carol = await UserIdAsync(factory, "carol");
        var company = await CreateUnitAsync(admin, "總公司");
        var it = await CreateUnitAsync(admin, "資訊處", company.Id);
        var closed = await CreateUnitAsync(admin, "已裁撤", company.Id);
        (await SaveMemberAsync(admin, it.Id, bob, primary: true, manager: true)).EnsureSuccessStatusCode();
        (await SaveMemberAsync(admin, closed.Id, carol, manager: true)).EnsureSuccessStatusCode();
        (await SaveMemberAsync(admin, it.Id, carol)).EnsureSuccessStatusCode();
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<Unit>().Where(x => x.Id == closed.Id).ExecuteUpdateAsync(p => p.SetProperty(x => x.Enabled, false));

        await DisableAsync(factory, bob);
        await DisableAsync(factory, carol);
        var item = (await ReviewsAsync(admin)).Users.Single(x => x.Id == bob).Items.Single();
        Assert.Equal(("organization.manager_vacant", "資訊處。"), (item.Code, item.Detail));
        Assert.Equal(FeatureCatalog.RouteOf(FeatureCatalog.Organization), item.Route);
        // 停用的單位不參與範圍，沒有空缺可言。
        Assert.Empty(await ReviewCodesAsync(admin, carol));

        var dave = await UserIdAsync(factory, "dave");
        (await SaveMemberAsync(admin, it.Id, dave, manager: true)).EnsureSuccessStatusCode();
        Assert.Empty(await ReviewCodesAsync(admin, bob));
    }
}
