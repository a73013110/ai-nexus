using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AiNexus.Features.Knowledge.Documents;

namespace AiNexus.Features.Knowledge.Indexing;

/// <summary>One indexed passage of a collection document; <see cref="SearchId"/> is the full-text key.</summary>
[Comment("結構化檢索片段、頁碼與內容指紋；查詢先套用資料 ACL。")]
public sealed class KnowledgeChunk
{
    [Comment("資料的主鍵識別碼。")]
    public Guid Id { get; set; } = Guid.NewGuid();
    [Comment("全文索引使用的整數唯一鍵；保留未來 ANN 映射。")]
    public int SearchId { get; set; }
    [Comment("關聯知識文件的識別碼。")]
    public Guid DocumentId { get; set; }
    [Comment("片段開始的原始文件頁碼。")]
    public int StartPage { get; set; }
    [Comment("片段結束的原始文件頁碼。")]
    public int EndPage { get; set; }
    [Comment("同一父物件內的呈現順序。")]
    public int Ordinal { get; set; }
    [Comment("由標題階層組成的結構路徑。")]
    public string HeadingPath { get; set; } = "";
    [Comment("文件頁面／片段的擷取文字。")]
    public string Text { get; set; } = "";
    [Comment("實際向量輸入（文件名稱、標題路徑與本文）的 SHA-256。")]
    public byte[] ContentHash { get; set; } = [];
    [Comment("依 CJK 與其他字元比例估算的片段 token 數。")]
    public int TokenEstimate { get; set; }
}

internal sealed class KnowledgeChunkConfiguration : IEntityTypeConfiguration<KnowledgeChunk>
{
    public void Configure(EntityTypeBuilder<KnowledgeChunk> chunk)
    {
        chunk.ToTable("Chunks", "knowledge"); chunk.HasKey(x => x.Id); chunk.Property(x => x.Text).HasMaxLength(4000);
        chunk.Property(x => x.HeadingPath).HasMaxLength(400); chunk.Property(x => x.ContentHash).HasColumnType("binary(32)");
        chunk.Property(x => x.SearchId).UseIdentityColumn();
        chunk.HasIndex(x => x.SearchId).IsUnique();
        chunk.HasIndex(x => new { x.DocumentId, x.Ordinal }).IsUnique(); chunk.HasOne<KnowledgeDocument>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
    }
}
