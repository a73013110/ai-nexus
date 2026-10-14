using AiNexus.Features.AccessControl;
using AiNexus.Features.AccessControl.Roles;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.Support;

internal static class PlatformGrants
{
    /// <summary>
    /// Separates platform operations from access management: the administrators group keeps access management only, and
    /// <paramref name="operatorId"/> gets platform operations alone through a role and group of its own.
    /// </summary>
    internal static async Task SplitOperationsAsync(NexusFactory factory, Guid operatorId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        db.Remove(await db.Set<RoleGroupFeature>().SingleAsync(x => x.GroupId == BuiltInAccess.AdministratorsGroup && x.FeatureId == FeatureIds.Operations));
        db.Set<Role>().Add(new() { Id = "operator", Name = "營運人員" });
        db.Set<RoleGroup>().Add(new() { Id = "operations-team", Name = "營運小組" });
        db.Set<RoleGroupRole>().Add(new() { RoleId = "operator", GroupId = "operations-team" });
        db.Set<RoleGroupFeature>().Add(new() { GroupId = "operations-team", FeatureId = FeatureIds.Operations });
        db.Set<UserRole>().Add(new() { UserId = operatorId, RoleId = "operator" });
        await db.SaveChangesAsync();
    }
}
