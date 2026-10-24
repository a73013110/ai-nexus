using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Features.Admin.AccessControl;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.IdentityApi;
using static AiNexus.IntegrationTests.Support.OrganizationApi;

namespace AiNexus.IntegrationTests.AccessControl;

public sealed class RoleHolderAccountReviewTests
{
    [Fact]
    public async Task DisabledUsersAreListedForEnabledRolesNoActiveUserHoldsAnyMore()
    {
        await using var factory = new AppFactory(administrators: ["alice"]); using var admin = await factory.SignedInAsync();
        // 測試身分不在 AD 夾具裡，讓目錄查不到以免混入「AD 找不到」的待辦。
        factory.AdDirectory.Unavailable = true;
        var bob = await UserIdAsync(factory, "bob");
        var carol = await UserIdAsync(factory, "carol");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Set<Role>().AddRange(new Role { Id = "payroll", Name = "薪資" }, new Role { Id = "retired", Name = "舊系統", Enabled = false });
            db.Set<UserRole>().AddRange(new UserRole { UserId = bob, RoleId = "payroll" }, new UserRole { UserId = bob, RoleId = "retired" });
            await db.SaveChangesAsync();
        }

        // 一般角色（member）還有其他啟用中的人，停用的角色不需要人持有。
        await DisableAsync(factory, bob);
        var item = (await ReviewsAsync(admin)).Users.Single(x => x.Id == bob).Items.Single();
        Assert.Equal(("access-control.role_orphaned", "薪資。"), (item.Code, item.Detail));
        Assert.Equal(FeatureCatalog.RouteOf(FeatureCatalog.AccessControlRoles), item.Route);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Set<UserRole>().Add(new UserRole { UserId = carol, RoleId = "payroll" }); await db.SaveChangesAsync();
        }
        Assert.Empty(await ReviewCodesAsync(admin, bob));
    }
}
