using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Attachments;

[Comment("站外附件原檔的 metadata、儲存識別、擷取文字及生命週期；不保存原始 bytes。")]
public sealed class Attachment
{
    public const int FileNameMaxLength = 180;

    [Comment("資料的主鍵識別碼。")]
    public Guid Id { get; set; } = Guid.NewGuid();
    [Comment("資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。")]
    public Guid OwnerId { get; set; }
    [Comment("原始附件檔名，不作為伺服器儲存路徑。")]
    public string FileName { get; set; } = "";
    [Comment("核准的 MIME 型別。")]
    public string ContentType { get; set; } = "";
    [Comment("原始附件大小，以 bytes 計。")]
    public long Size { get; set; }
    [Comment("站外原檔的不可變隨機識別碼；不含使用者路徑或檔名。")]
    public string StorageKey { get; set; } = Guid.NewGuid().ToString("N");
    [Comment("原檔儲存狀態 pending／ready／deleting；刪檔成功才釋放 metadata 與容量。")]
    public string StorageState { get; set; } = AttachmentStates.Ready;
    [Comment("附件分析後的文字。")]
    public string? ExtractedText { get; set; }
    [Comment("是否由個人檔案庫獨立保留原檔；移除對話或知識索引不會刪除保留的檔案。")]
    public bool InLibrary { get; set; }
    [Comment("資料建立時間，採 UTC offset。")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

internal sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> file)
    {
        file.ToTable("Attachments", "attachments", table =>
        {
            table.HasCheckConstraint("CK_Attachments_Size", "[Size] > 0");
            table.HasCheckConstraint("CK_Attachments_StorageState", "[StorageState] IN ('pending', 'ready', 'deleting')");
        });
        file.HasKey(x => x.Id);
        file.Property(x => x.FileName).HasMaxLength(Attachment.FileNameMaxLength);
        file.Property(x => x.ContentType).HasMaxLength(80);
        file.Property(x => x.StorageKey).HasMaxLength(32);
        file.Property(x => x.StorageState).HasMaxLength(16);
        file.HasIndex(x => x.StorageKey).IsUnique();
        file.HasIndex(x => new { x.StorageState, x.CreatedAt });
        file.HasIndex(x => new { x.OwnerId, x.CreatedAt });
        file.HasIndex(x => new { x.OwnerId, x.InLibrary, x.CreatedAt, x.Id });
    }
}
