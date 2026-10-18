using AiNexus.Data.App.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Collaboration;

public sealed class ResourceMember
{
    public Guid ResourceId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = "viewer";
}

internal sealed class ResourceMemberConfiguration : IEntityTypeConfiguration<ResourceMember>
{
    public void Configure(EntityTypeBuilder<ResourceMember> member)
    {
        member.ToTable("ResourceMembers", "collaboration");
        member.HasKey(x => new { x.ResourceId, x.UserId });
        member.Property(x => x.Role).HasMaxLength(12);
        member.HasOne<Resource>().WithMany().HasForeignKey(x => x.ResourceId).OnDelete(DeleteBehavior.Cascade);
        member.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
