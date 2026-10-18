using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Features.AccessControl;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.Support;

internal static class PlatformGrants
{
    /// <summary>
    /// 將平台營運與存取管理分開：administrators 群組只保留存取管理，而
    /// <paramref name="operatorId"/> 則透過自己的角色與群組，只取得平台營運權限。
    /// </summary>
    internal static async Task SplitOperationsAsync(AppFactory factory, Guid operatorId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Remove(await db.Set<RoleGroupFeature>().SingleAsync(x => x.GroupId == BuiltInAccess.AdministratorsGroup && x.FeatureId == FeatureIds.Operations));
        db.Set<Role>().Add(new() { Id = "operator", Name = "營運人員" });
        db.Set<RoleGroup>().Add(new() { Id = "operations-team", Name = "營運小組" });
        db.Set<RoleGroupRole>().Add(new() { RoleId = "operator", GroupId = "operations-team" });
        db.Set<RoleGroupFeature>().Add(new() { GroupId = "operations-team", FeatureId = FeatureIds.Operations });
        db.Set<UserRole>().Add(new() { UserId = operatorId, RoleId = "operator" });
        await db.SaveChangesAsync();
    }
}
