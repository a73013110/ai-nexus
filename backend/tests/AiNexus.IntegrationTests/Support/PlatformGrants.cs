using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Features.Admin.AccessControl;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.Support;

internal static class PlatformGrants
{
    /// <summary>原本「平台營運」授權拆出的頁面：平台用量（含全平台總覽與花費報表）、價格、檢索管理。</summary>
    internal static readonly string[] OperationsFeatures = [FeatureCatalog.OperationsUsage, FeatureCatalog.BillingPrices, FeatureCatalog.KnowledgeRetrieval];

    /// <summary>
    /// 將平台營運與存取管理分開：administrators 群組只保留存取管理等其他功能，而
    /// <paramref name="operatorId"/> 則透過自己的角色與群組，只取得平台營運的頁面。
    /// </summary>
    internal static async Task SplitOperationsAsync(AppFactory factory, Guid operatorId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.RemoveRange(await db.Set<RoleGroupFeature>().Where(x => x.GroupId == BuiltInAccess.AdministratorsGroup && OperationsFeatures.Contains(x.FeatureId)).ToListAsync());
        db.Set<Role>().Add(new() { Id = "operator", Name = "營運人員" });
        db.Set<RoleGroup>().Add(new() { Id = "operations-team", Name = "營運小組" });
        db.Set<RoleGroupRole>().Add(new() { RoleId = "operator", GroupId = "operations-team" });
        db.Set<RoleGroupFeature>().AddRange(OperationsFeatures.Select(feature => new RoleGroupFeature { GroupId = "operations-team", FeatureId = feature }));
        db.Set<UserRole>().Add(new() { UserId = operatorId, RoleId = "operator" });
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// 評測預設只給 administrators；評測的測試驗證的是題庫 ACL 與回饋歸屬，所以讓一般成員也有評測頁，
    /// 不必把每個帳號都設成管理員（管理員會多出不相干的授權）。
    /// </summary>
    internal static void QualityForMembers(AppDbContext db)
    {
        db.Set<RoleGroupFeature>().AddRange(new RoleGroupFeature { GroupId = BuiltInAccess.WorkspaceGroup, FeatureId = FeatureCatalog.Quality },
            new RoleGroupFeature { GroupId = BuiltInAccess.WorkspaceGroup, FeatureId = FeatureCatalog.QualityRetrieval });
        db.SaveChanges();
    }
}
