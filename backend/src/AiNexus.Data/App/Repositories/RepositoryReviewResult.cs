using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Repositories;

public sealed class RepositoryReviewResult
{
    public Guid ReviewId { get; set; }
    public int Ordinal { get; set; }
    public string Output { get; set; } = "";
    public bool Truncated { get; set; }
    public long? InputTokens { get; set; }
    public long? OutputTokens { get; set; }
    public long ElapsedMs { get; set; }
}

internal sealed class RepositoryReviewResultConfiguration : IEntityTypeConfiguration<RepositoryReviewResult>
{
    public void Configure(EntityTypeBuilder<RepositoryReviewResult> s)
    {
        s.ToTable("RepositoryReviewResults", "repositories");
        s.HasKey(x => new { x.ReviewId, x.Ordinal });
        s.Property(x => x.Output).HasMaxLength(64000);
        s.HasOne<RepositoryReview>().WithMany().HasForeignKey(x => x.ReviewId).OnDelete(DeleteBehavior.Restrict);
    }
}
