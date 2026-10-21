using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Inference;

/// <summary>
/// <c>inference.udf_UsageEntries</c> 的一列：聊天執行與模型工作合併後的用量紀錄，只讀。
/// 以 <see cref="AppDbContext.UsageEntries"/> 查詢，可以再組合篩選與彙總，整段仍是一次 SQL。
/// </summary>
public sealed class UsageEntry
{
    public Guid OwnerId { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>聊天執行為 <c>chat</c>，其他為 <see cref="ModelInvocation.Kind"/>。</summary>
    public string Kind { get; init; } = "";

    public string ModelId { get; init; } = "";

    public string Status { get; init; } = "";

    public long? InputTokens { get; init; }

    public long? OutputTokens { get; init; }

    public long? DurationMilliseconds { get; init; }
}

internal sealed class UsageEntryConfiguration : IEntityTypeConfiguration<UsageEntry>
{
    public void Configure(EntityTypeBuilder<UsageEntry> entry)
    {
        // 沒有表：只經由函式讀取。不設 ToTable(null) 的話 EF 會依慣例對應到不存在的 dbo.UsageEntry，啟動比對就會失敗。
        entry.HasNoKey().ToTable((string?)null);
        entry.Property(x => x.Kind).HasMaxLength(32);
        entry.Property(x => x.ModelId).HasMaxLength(160);
        entry.Property(x => x.Status).HasMaxLength(16);
    }
}
