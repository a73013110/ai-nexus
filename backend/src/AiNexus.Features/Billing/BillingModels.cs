using AiNexus.Features.Conversations;
using AiNexus.Features.Identity;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Billing;

// Prices are append-only versions. A charge copies the applicable tariff before dispatch.
public sealed class ModelPrice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Provider { get; set; } = "";
    public string ModelId { get; set; } = "";
    public string Currency { get; set; } = "USD";
    public string Kind { get; set; } = "api";
    public decimal InputPerMillion { get; set; }
    public decimal CachedInputPerMillion { get; set; }
    public decimal OutputPerMillion { get; set; }
    public decimal PerRequest { get; set; }
    public string RequestCharge { get; set; } = "completed";
    public string Note { get; set; } = "";
    public DateTimeOffset EffectiveAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid CreatedBy { get; set; }
}

public sealed class ModelCharge
{
    // Call ID is also the primary key: retries cannot create a second charge for a call.
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public Guid? ConversationId { get; set; }
    public string Provider { get; set; } = "";
    public string ModelId { get; set; } = "";
    public string Operation { get; set; } = "chat";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public Guid? PriceId { get; set; }
    public string Currency { get; set; } = "";
    public string Kind { get; set; } = "unpriced";
    public decimal InputPerMillion { get; set; }
    public decimal CachedInputPerMillion { get; set; }
    public decimal OutputPerMillion { get; set; }
    public decimal PerRequest { get; set; }
    public string RequestCharge { get; set; } = "completed";
    public long? InputTokens { get; set; }
    public long? CachedInputTokens { get; set; }
    // Output includes the provider's billed reasoning tokens; these are a subset, not extra.
    public long? OutputTokens { get; set; }
    public long? ReasoningTokens { get; set; }
    public bool UsageComplete { get; set; }
    public decimal? Amount { get; set; }
    public string State { get; set; } = "pending";
    public string Outcome { get; set; } = "queued";
}

public sealed record PriceRequest(string Provider, string ModelId, string Currency, string Kind,
    decimal InputPerMillion, decimal CachedInputPerMillion, decimal OutputPerMillion, decimal PerRequest,
    string RequestCharge, DateTimeOffset EffectiveAt, string Note);
public sealed record PriceDto(Guid Id, string Provider, string ModelId, string Currency, string Kind,
    decimal InputPerMillion, decimal CachedInputPerMillion, decimal OutputPerMillion, decimal PerRequest,
    string RequestCharge, DateTimeOffset EffectiveAt, string Note, string? ModelDisplayName = null);
public sealed record PriceTargetDto(string Provider, string ModelId, string DisplayName);
public sealed record ChargeDto(string State, string Kind, string Currency, decimal? Amount, long? InputTokens,
    long? CachedInputTokens, long? OutputTokens, long? ReasoningTokens);
public sealed record MoneyTotalDto(string Currency, string Kind, decimal Amount, int KnownCalls, int UnknownCalls);
public sealed record SpendBucketDto(string Label, string Currency, string Kind, decimal Amount, int Requests,
    int UnknownCalls, long InputTokens, long OutputTokens);
public sealed record SpendUserDto(Guid OwnerId, string DisplayName, string Account, string Currency, string Kind,
    decimal Amount, int Requests, int UnknownCalls);
public sealed record SpendReportDto(DateTimeOffset From, DateTimeOffset Until, int OffsetMinutes, int Requests,
    int PendingCalls, int LegacyCalls, long InputTokens, long OutputTokens, IReadOnlyList<MoneyTotalDto> Totals,
    IReadOnlyList<SpendBucketDto> Daily, IReadOnlyList<SpendBucketDto> Models, IReadOnlyList<SpendUserDto> Users);
public sealed record ConversationSpendDto(int Requests, int PendingCalls, int LegacyCalls, IReadOnlyList<MoneyTotalDto> Totals,
    IReadOnlyList<SpendBucketDto> Models);

public static class BillingConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        var price = model.Entity<ModelPrice>(); price.ToTable("ModelPrices", "inference"); price.HasKey(x => x.Id);
        price.HasIndex(x => new { x.Provider, x.ModelId, x.EffectiveAt }).IsUnique();
        price.Property(x => x.Provider).HasMaxLength(32); price.Property(x => x.ModelId).HasMaxLength(160);
        price.Property(x => x.Currency).HasMaxLength(3); price.Property(x => x.Kind).HasMaxLength(16);
        price.Property(x => x.RequestCharge).HasMaxLength(16); price.Property(x => x.Note).HasMaxLength(500);
        price.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        var charge = model.Entity<ModelCharge>(); charge.ToTable("ModelCharges", "inference"); charge.HasKey(x => x.Id);
        charge.HasIndex(x => new { x.OwnerId, x.CreatedAt }); charge.HasIndex(x => x.CreatedAt); charge.HasIndex(x => x.ConversationId);
        charge.Property(x => x.Provider).HasMaxLength(32); charge.Property(x => x.ModelId).HasMaxLength(160);
        charge.Property(x => x.Operation).HasMaxLength(32); charge.Property(x => x.Currency).HasMaxLength(3);
        charge.Property(x => x.Kind).HasMaxLength(16); charge.Property(x => x.RequestCharge).HasMaxLength(16);
        charge.Property(x => x.State).HasMaxLength(24); charge.Property(x => x.Outcome).HasMaxLength(16);
        charge.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        charge.HasOne<Conversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Restrict);
        charge.HasOne<ModelPrice>().WithMany().HasForeignKey(x => x.PriceId).OnDelete(DeleteBehavior.Restrict);
        foreach (var entity in new[] { typeof(ModelPrice), typeof(ModelCharge) })
            foreach (var name in new[] { "InputPerMillion", "CachedInputPerMillion", "OutputPerMillion", "PerRequest" })
                model.Entity(entity).Property<decimal>(name).HasPrecision(20, 8);
        charge.Property(x => x.Amount).HasPrecision(20, 8);
    }
}
