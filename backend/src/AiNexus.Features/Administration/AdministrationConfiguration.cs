using AiNexus.Features.AccessControl;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Administration;

/// <summary>The administrator role, group and feature, and the module's tables. <see cref="Configure"/> is the entry point kept for <c>NexusDbContext</c>.</summary>
public static class AdministrationConfiguration
{
    public const string Role = "administrator", Group = "administrators", Feature = FeatureIds.Admin, Policy = Policies.Admin;
    public static void Configure(ModelBuilder model)
    {
        model.Entity<AccessControl.Role>().HasData(new AccessControl.Role { Id = Role, Name = "平台管理員" });
        model.Entity<RoleGroup>().HasData(new RoleGroup { Id = Group, Name = "平台管理" });
        model.Entity<AccessControl.Feature>().HasData(new AccessControl.Feature { Id = Feature, Name = "平台管理", Route = "/admin", SortOrder = 90 });
        model.Entity<RoleGroupRole>().HasData(new RoleGroupRole { RoleId = Role, GroupId = Group });
        model.Entity<RoleGroupFeature>().HasData(new RoleGroupFeature { GroupId = Group, FeatureId = Feature });
        model.ApplyConfiguration(new AdministratorBootstrapConfiguration());
    }
}
