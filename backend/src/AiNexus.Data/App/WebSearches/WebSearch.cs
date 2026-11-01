using AiNexus.Data.App.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.WebSearches;

public sealed class WebSearch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public Guid ConversationId { get; set; }
    public string IdempotencyKey { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public string Status { get; set; } = "running";
    public string ResultsJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? RunId { get; set; }
}

internal sealed class WebSearchConfiguration : IEntityTypeConfiguration<WebSearch>
{
    public void Configure(EntityTypeBuilder<WebSearch> item)
    {
        item.Property(x => x.CreatedAt).HasValueGenerator<CreationTime>();
        item.ToTable("WebSearches", "websearches");
        item.HasKey(x => x.Id);
        item.Property(x => x.IdempotencyKey).HasMaxLength(80);
        item.Property(x => x.RequestHash).HasMaxLength(64);
        item.Property(x => x.Status).HasMaxLength(16);
        item.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
    }
}
