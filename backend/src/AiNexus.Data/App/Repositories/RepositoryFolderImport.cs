using AiNexus.Data.App.Identity;
using AiNexus.Data.App.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Repositories;

/// <summary>資料夾快照匯入的紀錄：申請時選定的檔案清單，由背景工作逐一匯入。</summary>
public sealed class RepositoryFolderImport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public Guid JobId { get; set; }
    public string BaseUrl { get; set; } = "";
    public string Repository { get; set; } = "";
    public string Commit { get; set; } = "";
    public string Path { get; set; } = "";
    public Guid CollectionId { get; set; }
    public string PathsJson { get; set; } = "[]";
    public int Imported { get; set; }
    public int Skipped { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class RepositoryFolderImportConfiguration : IEntityTypeConfiguration<RepositoryFolderImport>
{
    public void Configure(EntityTypeBuilder<RepositoryFolderImport> i)
    {
        i.Property(x => x.CreatedAt).HasValueGenerator<CreationTime>();
        i.ToTable("RepositoryFolderImports", "repositories");
        i.HasKey(x => x.Id);
        i.Property(x => x.BaseUrl).HasMaxLength(500);
        i.Property(x => x.Repository).HasMaxLength(201);
        i.Property(x => x.Commit).HasMaxLength(64);
        i.Property(x => x.Path).HasMaxLength(500);
        i.Property(x => x.PathsJson).HasMaxLength(600000);
        i.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        i.HasOne<BackgroundJob>().WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Restrict);
    }
}
