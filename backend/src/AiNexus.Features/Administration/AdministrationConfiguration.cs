using AiNexus.Features.AccessControl;
using AiNexus.Features.Identity;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Administration;

public sealed class AdministrationOptions { public string[] BootstrapAdministrators { get; set; } = []; }
public sealed class AdministrativeWriteLock { public SemaphoreSlim Gate { get; } = new(1, 1); }
public sealed class AdministratorBootstrap
{
    public Guid UserId { get; set; }
    public DateTimeOffset GrantedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class GroupModelPolicy
{
    public string GroupId { get; set; } = "";
    public string? AllowedModelsJson { get; set; }
    public string? DailyTokenLimitsJson { get; set; }
    public long? StoredAttachmentLimitBytes { get; set; }
}

public sealed class UserModelPolicy
{
    public Guid UserId { get; set; }
    public string? AllowedModelsJson { get; set; }
    public string? DailyTokenLimitsJson { get; set; }
}

public static class AdministrationConfiguration
{
    public const string Role = "administrator", Group = "administrators", Feature = "admin", Policy = Policies.Prefix + Feature;
    public static void Configure(ModelBuilder model)
    {
        model.Entity<AccessControl.Role>().HasData(new AccessControl.Role { Id = Role, Name = "平台管理員" });
        model.Entity<RoleGroup>().HasData(new RoleGroup { Id = Group, Name = "平台管理" });
        model.Entity<AccessControl.Feature>().HasData(new AccessControl.Feature { Id = Feature, Name = "平台管理", Route = "/admin", SortOrder = 90 });
        model.Entity<RoleGroupRole>().HasData(new RoleGroupRole { RoleId = Role, GroupId = Group });
        model.Entity<RoleGroupFeature>().HasData(new RoleGroupFeature { GroupId = Group, FeatureId = Feature });
        var bootstrap = model.Entity<AdministratorBootstrap>();
        bootstrap.ToTable("AdministratorBootstraps", "access"); bootstrap.HasKey(x => x.UserId);
        bootstrap.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        var policy = model.Entity<GroupModelPolicy>();
        policy.ToTable("GroupModelPolicies", "access"); policy.HasKey(x => x.GroupId);
        policy.Property(x => x.GroupId).HasMaxLength(64); policy.Property(x => x.AllowedModelsJson).HasMaxLength(4000); policy.Property(x => x.DailyTokenLimitsJson).HasMaxLength(8000);
        policy.HasOne<RoleGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Cascade);
        var personal = model.Entity<UserModelPolicy>();
        personal.ToTable("UserModelPolicies", "access"); personal.HasKey(x => x.UserId);
        personal.Property(x => x.AllowedModelsJson).HasMaxLength(4000); personal.Property(x => x.DailyTokenLimitsJson).HasMaxLength(8000);
        personal.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
