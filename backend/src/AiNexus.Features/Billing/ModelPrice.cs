using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

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

internal sealed class ModelPriceConfiguration : IEntityTypeConfiguration<ModelPrice>
{
    public void Configure(EntityTypeBuilder<ModelPrice> price)
    {
        price.ToTable("ModelPrices", "inference"); price.HasKey(x => x.Id);
        price.HasIndex(x => new { x.Provider, x.ModelId, x.EffectiveAt }).IsUnique();
        price.Property(x => x.Provider).HasMaxLength(32); price.Property(x => x.ModelId).HasMaxLength(160);
        price.Property(x => x.Currency).HasMaxLength(3); price.Property(x => x.Kind).HasMaxLength(16);
        price.Property(x => x.RequestCharge).HasMaxLength(16); price.Property(x => x.Note).HasMaxLength(500);
        price.Property(x => x.InputPerMillion).HasPrecision(20, 8); price.Property(x => x.CachedInputPerMillion).HasPrecision(20, 8);
        price.Property(x => x.OutputPerMillion).HasPrecision(20, 8); price.Property(x => x.PerRequest).HasPrecision(20, 8);
    }
}
