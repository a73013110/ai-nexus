using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Repositories;

/// <summary>Provenance of a knowledge document imported as a snapshot of one file at a pinned commit.</summary>
[Comment("程式庫文件匯入所固定的主機、repository、commit 與檔案路徑。")]
public sealed class RepositoryImport
{
    [Comment("資料的主鍵識別碼。")]
    public Guid Id { get; set; } = Guid.NewGuid();
    [Comment("資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。")]
    public Guid OwnerId { get; set; }
    [Comment("關聯知識庫的識別碼。")]
    public Guid CollectionId { get; set; }
    [Comment("關聯知識文件的識別碼。")]
    public Guid DocumentId { get; set; }
    [Comment("Gitea repository 的 owner/name 識別。")]
    public string Repository { get; set; } = "";
    [Comment("repository 內的檔案路徑，不是伺服器路徑。")]
    public string Path { get; set; } = "";
    [Comment("匯入當時固定的 commit SHA。")]
    public string Commit { get; set; } = "";
    [Comment("Gitea 連線主機位址。")]
    public string BaseUrl { get; set; } = "";
}

internal sealed class RepositoryImportConfiguration : IEntityTypeConfiguration<RepositoryImport>
{
    public void Configure(EntityTypeBuilder<RepositoryImport> i)
    {
        i.ToTable("RepositoryImports", "repositories"); i.HasKey(x => x.Id);
        i.Property(x => x.Repository).HasMaxLength(201); i.Property(x => x.Path).HasMaxLength(500); i.Property(x => x.Commit).HasMaxLength(64); i.Property(x => x.BaseUrl).HasMaxLength(500);
        i.HasIndex(x => new { x.OwnerId, x.CollectionId, x.Repository, x.Commit, x.Path });
    }
}
