using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Inference;

/// <summary>One non-chat model call (OCR, text transformation, evaluation, review) for quota and usage accounting.</summary>
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
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

internal sealed class ModelInvocationConfiguration : IEntityTypeConfiguration<ModelInvocation>
{
    public void Configure(EntityTypeBuilder<ModelInvocation> item)
    {
        item.ToTable("ModelInvocations", "inference"); item.HasKey(x => x.Id);
        item.Property(x => x.Kind).HasMaxLength(32); item.Property(x => x.ModelId).HasMaxLength(160); item.Property(x => x.Status).HasMaxLength(16);
        item.Property(x => x.Provider).HasMaxLength(32);
        item.HasIndex(x => new { x.OwnerId, x.CreatedAt });
    }
}
