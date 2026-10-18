using AiNexus.Data.App.Knowledge;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Repositories;

/// <summary>知識文件的來源紀錄：由某個固定 commit 上的一個檔案快照匯入。</summary>
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
        i.ToTable("RepositoryImports", "repositories");
        i.HasKey(x => x.Id);
        i.Property(x => x.Repository).HasMaxLength(201);
        i.Property(x => x.Path).HasMaxLength(500);
        i.Property(x => x.Commit).HasMaxLength(64);
        i.Property(x => x.BaseUrl).HasMaxLength(500);
        i.HasOne<Document>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
    }
}
