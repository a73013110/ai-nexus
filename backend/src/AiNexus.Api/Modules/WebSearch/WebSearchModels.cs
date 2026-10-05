using AiNexus.Modules.Identity;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Modules.WebSearch;

public sealed class WebSearchOptions
{
    public bool Enabled { get; set; }
    public string Provider { get; set; } = "searxng";
    public string Endpoint { get; set; } = "http://localhost:8080/";
    public string ApiKey { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 10;
    public int MaxResults { get; set; } = 5;
    public int MaxDailyRequests { get; set; } = 100;
}
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
public sealed record WebSourceDto(int Number, string Title, string Url, string Excerpt, DateTimeOffset RetrievedAt);
public sealed record WebSearchStatusDto(bool Available, string Notice);
public static class WebSearchConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        var item = model.Entity<WebSearchRecord>(); item.ToTable("WebSearches", "inference"); item.HasKey(x => x.Id);
        item.Property(x => x.IdempotencyKey).HasMaxLength(80); item.Property(x => x.RequestHash).HasMaxLength(64);
        item.Property(x => x.Status).HasMaxLength(16); item.HasIndex(x => new { x.OwnerId, x.IdempotencyKey }).IsUnique();
        item.HasIndex(x => x.RunId); item.HasIndex(x => new { x.OwnerId, x.CreatedAt });
        item.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
    }
}
