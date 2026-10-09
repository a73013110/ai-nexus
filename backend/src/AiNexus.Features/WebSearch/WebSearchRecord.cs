using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.WebSearch;

public sealed class WebSearchRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public Guid ConversationId { get; set; }
    public string IdempotencyKey { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public string Status { get; set; } = "running";
    public string ResultsJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? RunId { get; set; }
}

internal sealed class WebSearchRecordConfiguration : IEntityTypeConfiguration<WebSearchRecord>
{
    public void Configure(EntityTypeBuilder<WebSearchRecord> item)
    {
        item.ToTable("WebSearches", "inference"); item.HasKey(x => x.Id);
        item.Property(x => x.IdempotencyKey).HasMaxLength(80); item.Property(x => x.RequestHash).HasMaxLength(64);
        item.Property(x => x.Status).HasMaxLength(16); item.HasIndex(x => new { x.OwnerId, x.IdempotencyKey }).IsUnique();
        item.HasIndex(x => x.RunId); item.HasIndex(x => new { x.OwnerId, x.CreatedAt });
    }
}
