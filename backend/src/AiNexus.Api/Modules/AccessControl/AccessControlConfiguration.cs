using AiNexus.Modules.Identity;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Modules.AccessControl;

public static class AccessControlConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        var role = model.Entity<Role>();
        role.ToTable("Roles", "access");
        role.HasKey(x => x.Id);
        role.Property(x => x.Id).HasMaxLength(64);
        role.Property(x => x.Name).HasMaxLength(120);
        role.HasData(new Role { Id = BuiltInAccess.MemberRole, Name = "一般使用者" });

        var group = model.Entity<RoleGroup>();
        group.ToTable("RoleGroups", "access");
        group.HasKey(x => x.Id);
        group.Property(x => x.Id).HasMaxLength(64);
        group.Property(x => x.Name).HasMaxLength(120);
        group.HasData(new RoleGroup { Id = BuiltInAccess.WorkspaceGroup, Name = "基本工作區" });

        var feature = model.Entity<Feature>();
        feature.ToTable("Features", "access");
        feature.HasKey(x => x.Id);
        feature.Property(x => x.Id).HasMaxLength(64);
        feature.Property(x => x.Name).HasMaxLength(120);
        feature.Property(x => x.Route).HasMaxLength(160);
        feature.HasData(new Feature { Id = BuiltInAccess.ChatFeature, Name = "AI 對話", Route = "/chat", SortOrder = 10 });

        var userRole = model.Entity<UserRole>();
        userRole.ToTable("UserRoles", "access");
        userRole.HasKey(x => new { x.UserId, x.RoleId });
        userRole.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        userRole.HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);

        var membership = model.Entity<RoleGroupRole>();
        membership.ToTable("RoleGroupRoles", "access");
        membership.HasKey(x => new { x.RoleId, x.GroupId });
        membership.HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        membership.HasOne<RoleGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
        membership.HasData(new RoleGroupRole { RoleId = BuiltInAccess.MemberRole, GroupId = BuiltInAccess.WorkspaceGroup });

        var grant = model.Entity<RoleGroupFeature>();
        grant.ToTable("RoleGroupFeatures", "access");
        grant.HasKey(x => new { x.GroupId, x.FeatureId });
        grant.HasOne<RoleGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Cascade);
        grant.HasOne<Feature>().WithMany().HasForeignKey(x => x.FeatureId).OnDelete(DeleteBehavior.Restrict);
        grant.HasData(new RoleGroupFeature { GroupId = BuiltInAccess.WorkspaceGroup, FeatureId = BuiltInAccess.ChatFeature });
    }
}
