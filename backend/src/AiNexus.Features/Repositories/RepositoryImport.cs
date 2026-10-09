using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Repositories;

/// <summary>Provenance of a knowledge document imported as a snapshot of one file at a pinned commit.</summary>
public sealed class RepositoryImport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public Guid CollectionId { get; set; }
    public Guid DocumentId { get; set; }
    public string Repository { get; set; } = "";
    public string Path { get; set; } = "";
    public string Commit { get; set; } = "";
    public string BaseUrl { get; set; } = "";
}

internal sealed class RepositoryImportConfiguration : IEntityTypeConfiguration<RepositoryImport>
{
    public void Configure(EntityTypeBuilder<RepositoryImport> i)
    {
        i.ToTable("RepositoryImports", "knowledge"); i.HasKey(x => x.Id);
        i.Property(x => x.Repository).HasMaxLength(201); i.Property(x => x.Path).HasMaxLength(500); i.Property(x => x.Commit).HasMaxLength(64); i.Property(x => x.BaseUrl).HasMaxLength(500);
        i.HasIndex(x => new { x.OwnerId, x.CollectionId, x.Repository, x.Commit, x.Path });
    }
}
