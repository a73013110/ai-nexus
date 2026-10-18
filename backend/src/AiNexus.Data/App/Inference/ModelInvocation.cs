using AiNexus.Data.App.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Inference;

/// <summary>
/// 非交談類模型呼叫紀錄實體（如 OCR、文字轉換、程式碼審查、模型評估等），用於配額扣減與用量審計。
/// </summary>
public sealed class ModelInvocation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OwnerId { get; set; }

    public string Kind { get; set; } = "";

    public string ModelId { get; set; } = "";

    public string Provider { get; set; } = "google";

    public long? DurationMilliseconds { get; set; }

    public string Status { get; set; } = "running";

    public long ReservedTokens { get; set; }

    public long? InputTokens { get; set; }

    public long? OutputTokens { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class ModelInvocationConfiguration : IEntityTypeConfiguration<ModelInvocation>
{
    public void Configure(EntityTypeBuilder<ModelInvocation> item)
    {
        item.ToTable("ModelInvocations", "inference");
        item.HasKey(x => x.Id);
        item.Property(x => x.CreatedAt).HasValueGenerator<CreationTime>();
        item.Property(x => x.Kind).HasMaxLength(32);
        item.Property(x => x.ModelId).HasMaxLength(160);
        item.Property(x => x.Status).HasMaxLength(16);
        item.Property(x => x.Provider).HasMaxLength(32);
        item.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
    }
}
