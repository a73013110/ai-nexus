using AiNexus.Data.App.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Billing;

// 價格只新增、不修改。呼叫在派送前複製當時適用的價格，之後的價格變動不影響已發出的呼叫。
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
    public DateTimeOffset EffectiveAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
}

internal sealed class ModelPriceConfiguration : IEntityTypeConfiguration<ModelPrice>
{
    public void Configure(EntityTypeBuilder<ModelPrice> price)
    {
        price.Property(x => x.EffectiveAt).HasValueGenerator<CreationTime>();
        price.Property(x => x.CreatedAt).HasValueGenerator<CreationTime>();
        price.ToTable("ModelPrices", "billing");
        price.HasKey(x => x.Id);
        price.Property(x => x.Provider).HasMaxLength(32);
        price.Property(x => x.ModelId).HasMaxLength(160);
        price.Property(x => x.Currency).HasMaxLength(3);
        price.Property(x => x.Kind).HasMaxLength(16);
        price.Property(x => x.RequestCharge).HasMaxLength(16);
        price.Property(x => x.Note).HasMaxLength(500);
        price.Property(x => x.InputPerMillion).HasPrecision(20, 8);
        price.Property(x => x.CachedInputPerMillion).HasPrecision(20, 8);
        price.Property(x => x.OutputPerMillion).HasPrecision(20, 8);
        price.Property(x => x.PerRequest).HasPrecision(20, 8);
        price.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
