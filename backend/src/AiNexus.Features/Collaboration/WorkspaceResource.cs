using AiNexus.Features.AccessControl;
using AiNexus.Features.Identity;
using AiNexus.Platform.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Collaboration;

// Shared ACL for knowledge collections, projects and editable artifacts.
public sealed class WorkspaceResource
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string Kind { get; set; } = "";
    public string Name { get; set; } = "";
    public Guid? ParentId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class ResourceMember
{
    public Guid ResourceId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = "viewer";
}
public sealed class ResourceGroup
{
    public Guid ResourceId { get; set; }
    public string GroupId { get; set; } = "";
}
public sealed record ResourceDto(Guid Id, string Name, string Kind, bool CanEdit, bool IsOwner, DateTimeOffset UpdatedAt);
public sealed record DirectoryUserDto(Guid Id, string Account, string DisplayName);
public sealed record DirectoryGroupDto(string Id, string Name);
public sealed record NamedResourceRequest(string Name);

internal sealed class WorkspaceResourceConfiguration : IEntityTypeConfiguration<WorkspaceResource>
{
    public void Configure(EntityTypeBuilder<WorkspaceResource> resource)
    {
        resource.ToTable("Resources", "collaboration"); resource.HasKey(x => x.Id);
        resource.Property(x => x.Kind).HasMaxLength(24); resource.Property(x => x.Name).HasMaxLength(120);
        resource.HasIndex(x => new { x.OwnerId, x.Kind, x.UpdatedAt });
        resource.HasQueryFilter(SoftDelete.Filter, x => !x.IsDeleted);
        resource.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        resource.HasOne<WorkspaceResource>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ResourceMemberConfiguration : IEntityTypeConfiguration<ResourceMember>
{
    public void Configure(EntityTypeBuilder<ResourceMember> member)
    {
        member.ToTable("ResourceMembers", "collaboration"); member.HasKey(x => new { x.ResourceId, x.UserId }); member.Property(x => x.Role).HasMaxLength(12);
        member.HasOne<WorkspaceResource>().WithMany().HasForeignKey(x => x.ResourceId).OnDelete(DeleteBehavior.Cascade);
        member.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ResourceGroupConfiguration : IEntityTypeConfiguration<ResourceGroup>
{
    public void Configure(EntityTypeBuilder<ResourceGroup> group)
    {
        group.ToTable("ResourceGroups", "collaboration"); group.HasKey(x => new { x.ResourceId, x.GroupId }); group.Property(x => x.GroupId).HasMaxLength(64);
        group.HasOne<WorkspaceResource>().WithMany().HasForeignKey(x => x.ResourceId).OnDelete(DeleteBehavior.Cascade);
        group.HasOne<RoleGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>The module's tables, applied by <c>NexusDbContext</c>.</summary>
public static class CollaborationConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        model.ApplyConfiguration(new WorkspaceResourceConfiguration());
        model.ApplyConfiguration(new ResourceMemberConfiguration());
        model.ApplyConfiguration(new ResourceGroupConfiguration());
    }
}
