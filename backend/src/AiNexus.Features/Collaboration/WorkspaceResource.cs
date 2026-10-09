using AiNexus.Platform.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Collaboration;

// Shared ACL for knowledge collections, projects and editable artifacts.
[Comment("共用資源的擁有者、種類、階層及版本；作為資料 ACL 邊界。")]
public sealed class WorkspaceResource
{
    [Comment("資料的主鍵識別碼。")]
    public Guid Id { get; set; } = Guid.NewGuid();
    [Comment("資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。")]
    public Guid OwnerId { get; set; }
    [Comment("資源種類。")]
    public string Kind { get; set; } = "";
    [Comment("業務物件的顯示名稱。")]
    public string Name { get; set; } = "";
    [Comment("父資源識別碼；子資源繼承父資源的 ACL。")]
    public Guid? ParentId { get; set; }
    [Comment("是否邏輯刪除；不自動刪除歷史紀錄。")]
    public bool IsDeleted { get; set; }
    [Comment("資料建立時間，採 UTC offset。")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    [Comment("資料最後修改時間，採 UTC offset。")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
[Comment("資源對具名使用者授予的閱讀或編輯權限。")]
public sealed class ResourceMember
{
    [Comment("關聯 Resources 的識別碼。")]
    public Guid ResourceId { get; set; }
    [Comment("關聯使用者的 Users 主鍵。")]
    public Guid UserId { get; set; }
    [Comment("具名成員的權限：viewer 或 editor。")]
    public string Role { get; set; } = "viewer";
}
[Comment("資源對功能群組授予的唯讀權限。")]
public sealed class ResourceGroup
{
    [Comment("關聯 Resources 的識別碼。")]
    public Guid ResourceId { get; set; }
    [Comment("關聯功能群組的識別碼。")]
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
        resource.HasOne<WorkspaceResource>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ResourceMemberConfiguration : IEntityTypeConfiguration<ResourceMember>
{
    public void Configure(EntityTypeBuilder<ResourceMember> member)
    {
        member.ToTable("ResourceMembers", "collaboration"); member.HasKey(x => new { x.ResourceId, x.UserId }); member.Property(x => x.Role).HasMaxLength(12);
        member.HasOne<WorkspaceResource>().WithMany().HasForeignKey(x => x.ResourceId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ResourceGroupConfiguration : IEntityTypeConfiguration<ResourceGroup>
{
    public void Configure(EntityTypeBuilder<ResourceGroup> group)
    {
        group.ToTable("ResourceGroups", "collaboration"); group.HasKey(x => new { x.ResourceId, x.GroupId }); group.Property(x => x.GroupId).HasMaxLength(64);
        group.HasOne<WorkspaceResource>().WithMany().HasForeignKey(x => x.ResourceId).OnDelete(DeleteBehavior.Cascade);
    }
}
