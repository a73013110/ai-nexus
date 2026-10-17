using AiNexus.Features.Collaboration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AiNexus.Features.Knowledge.Collections;

namespace AiNexus.Features.Knowledge.Documents;

/// <summary>
/// 來源文件：屬於某個知識庫（<see cref="CollectionId"/>），或屬於對話／專案的獨立文件。
/// 名稱、擁有者與專案存在同 Id 的 <see cref="WorkspaceResource"/>。純文字來源保留可編輯內容，
/// 並以 <see cref="TextVersion"/> 防止並行編輯互相覆蓋。
/// </summary>
[Comment("知識庫文件的原始附件、分析／索引狀態與 embedding profile。")]
public sealed class KnowledgeDocument
{
    public const string Kind = "document";

    [Comment("資料的主鍵識別碼。")]
    public Guid Id { get; set; }
    [Comment("關聯知識庫的識別碼。")]
    public Guid? CollectionId { get; set; }
    [Comment("引用的附件識別碼。")]
    public Guid? AttachmentId { get; set; }
    [Comment("原始附件檔名，不作為伺服器儲存路徑。")]
    public string FileName { get; set; } = "";
    [Comment("核准的 MIME 型別。")]
    public string ContentType { get; set; } = "";
    [Comment("業務執行狀態。")]
    public string Status { get; set; } = "queued";
    [Comment("文件總頁數。")]
    public int PageCount { get; set; }
    [Comment("文件已建立的檢索片段數。")]
    public int ChunkCount { get; set; }
    [Comment("處理過程中的非致命提示。")]
    public string? Warning { get; set; }
    [Comment("是否邏輯刪除；不自動刪除歷史紀錄。")]
    public bool IsDeleted { get; set; }
    [Comment("關聯背景工作識別碼。")]
    public Guid? JobId { get; set; }
    [Comment("純文字來源的可編輯內容；一般上傳原檔保持空值。")]
    public string? TextContent { get; set; }
    [Comment("純文字內容的樂觀並行版本號。")]
    public int TextVersion { get; set; }
}

internal sealed class KnowledgeDocumentConfiguration : IEntityTypeConfiguration<KnowledgeDocument>
{
    public void Configure(EntityTypeBuilder<KnowledgeDocument> document)
    {
        document.ToTable("Documents", "knowledge"); document.HasKey(x => x.Id);
        document.Property(x => x.FileName).HasMaxLength(180); document.Property(x => x.ContentType).HasMaxLength(80); document.Property(x => x.Status).HasMaxLength(16); document.Property(x => x.Warning).HasMaxLength(500);
        document.Property(x => x.TextContent).HasMaxLength(200000); document.Property(x => x.TextVersion).IsConcurrencyToken();
        document.HasIndex(x => new { x.CollectionId, x.Status, x.IsDeleted }).IncludeProperties(x => new { x.Id, x.FileName, x.ChunkCount }); document.HasIndex(x => x.AttachmentId);
        document.HasOne<KnowledgeCollection>().WithMany().HasForeignKey(x => x.CollectionId).OnDelete(DeleteBehavior.Restrict);
    }
}
