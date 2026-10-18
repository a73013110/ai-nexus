using AiNexus.Data.App.Attachments;
using AiNexus.Data.App.Collaboration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Knowledge;

/// <summary>
/// 來源文件：屬於某個知識庫（<see cref="CollectionId"/>），或屬於對話／專案的獨立文件。
/// 名稱、擁有者與專案存在同 Id 的 <see cref="Resource"/>。純文字來源保留可編輯內容，
/// 並以 <see cref="TextVersion"/> 防止並行編輯互相覆蓋。
/// </summary>
public sealed class Document
{
    public const string Kind = "document";

    public Guid Id { get; set; }
    public Guid? CollectionId { get; set; }
    public Guid? AttachmentId { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string Status { get; set; } = "queued";
    public int PageCount { get; set; }
    public int ChunkCount { get; set; }
    public string? Warning { get; set; }
    public bool IsDeleted { get; set; }
    public Guid? JobId { get; set; }
    public string? TextContent { get; set; }
    public int TextVersion { get; set; }
}

internal sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> document)
    {
        document.ToTable("Documents", "knowledge");
        document.HasKey(x => x.Id);
        document.Property(x => x.FileName).HasMaxLength(180);
        document.Property(x => x.ContentType).HasMaxLength(80);
        document.Property(x => x.Status).HasMaxLength(16);
        document.Property(x => x.Warning).HasMaxLength(500);
        document.Property(x => x.TextContent).HasMaxLength(200000);
        document.Property(x => x.TextVersion).IsConcurrencyToken();
        document.HasOne<Collection>().WithMany().HasForeignKey(x => x.CollectionId).OnDelete(DeleteBehavior.Restrict);
        document.HasOne<Resource>().WithMany().HasForeignKey(x => x.Id).OnDelete(DeleteBehavior.Restrict);
        document.HasOne<Attachment>().WithMany().HasForeignKey(x => x.AttachmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
