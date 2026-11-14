using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Repositories;

/// <summary>同步時在 Gitea 讀到的協作者（實際狀態），每輪整庫改寫；與授權比對出未登記存取。</summary>
public sealed class RepositoryCollaborator
{
    public Guid RepositoryId { get; set; }
    public string Login { get; set; } = "";
    public string Permission { get; set; } = "";
    public DateTimeOffset ObservedAt { get; set; }
}

internal sealed class RepositoryCollaboratorConfiguration : IEntityTypeConfiguration<RepositoryCollaborator>
{
    public void Configure(EntityTypeBuilder<RepositoryCollaborator> collaborator)
    {
        collaborator.ToTable("RepositoryCollaborators", "repositories");
        collaborator.HasKey(x => new { x.RepositoryId, x.Login });
        collaborator.Property(x => x.Login).HasMaxLength(RepositoryGrant.LoginMaxLength);
        collaborator.Property(x => x.Permission).HasMaxLength(16);
        collaborator.HasOne<ManagedRepository>().WithMany().HasForeignKey(x => x.RepositoryId).OnDelete(DeleteBehavior.Restrict);
    }
}
