using AiNexus.Data.App.Identity;
using AiNexus.Platform.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Collaboration;

// 知識庫、專案與可編輯成果共用的資源表，是資料層的 ACL 邊界。
public sealed class Resource
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string Kind { get; set; } = "";
    public string Name { get; set; } = "";
    public Guid? ParentId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class ResourceConfiguration : IEntityTypeConfiguration<Resource>
{
    public void Configure(EntityTypeBuilder<Resource> resource)
    {
        resource.Property(x => x.CreatedAt).HasValueGenerator<CreationTime>();
        resource.Property(x => x.UpdatedAt).HasValueGenerator<CreationTime>();
        resource.ToTable("Resources", "collaboration");
        resource.HasKey(x => x.Id);
        resource.Property(x => x.Kind).HasMaxLength(24);
        resource.Property(x => x.Name).HasMaxLength(120);
        resource.HasQueryFilter(SoftDelete.Filter, x => !x.IsDeleted);
        resource.HasOne<Resource>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        resource.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
    }
}
