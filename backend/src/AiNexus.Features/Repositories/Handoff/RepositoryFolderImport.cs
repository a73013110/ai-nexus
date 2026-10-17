using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Repositories.Handoff;

/// <summary>資料夾快照匯入的紀錄：申請時選定的檔案清單，由背景工作逐一匯入。</summary>
[Comment("程式庫資料夾匯入知識庫的固定版本、檔案清單與背景工作。")]
public sealed class RepositoryFolderImport
{
    [Comment("資料的主鍵識別碼。")]
    public Guid Id { get; set; } = Guid.NewGuid();
    [Comment("資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。")]
    public Guid OwnerId { get; set; }
    [Comment("關聯背景工作識別碼。")]
    public Guid JobId { get; set; }
    [Comment("Gitea 連線主機位址。")]
    public string BaseUrl { get; set; } = "";
    [Comment("Gitea repository 的 owner/name 識別。")]
    public string Repository { get; set; } = "";
    [Comment("匯入當時固定的 commit SHA。")]
    public string Commit { get; set; } = "";
    [Comment("repository 內的資料夾路徑；空字串為根目錄。")]
    public string Path { get; set; } = "";
    [Comment("關聯知識庫的識別碼。")]
    public Guid CollectionId { get; set; }
    [Comment("建立時選定的檔案路徑 JSON 陣列。")]
    public string PathsJson { get; set; } = "[]";
    [Comment("已匯入或沿用的檔案數。")]
    public int Imported { get; set; }
    [Comment("因非文字或超過上限而略過的檔案數。")]
    public int Skipped { get; set; }
    [Comment("資料建立時間，採 UTC offset。")]
    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class RepositoryFolderImportConfiguration : IEntityTypeConfiguration<RepositoryFolderImport>
{
    public void Configure(EntityTypeBuilder<RepositoryFolderImport> i)
    {
        i.Property(x => x.CreatedAt).HasValueGenerator<Persistence.CreationTime>();
        i.ToTable("RepositoryFolderImports", "repositories");
        i.HasKey(x => x.Id);
        i.Property(x => x.BaseUrl).HasMaxLength(500);
        i.Property(x => x.Repository).HasMaxLength(201);
        i.Property(x => x.Commit).HasMaxLength(64);
        i.Property(x => x.Path).HasMaxLength(500);
        i.Property(x => x.PathsJson).HasMaxLength(600000);
        i.HasIndex(x => new { x.OwnerId, x.CreatedAt });
    }
}
