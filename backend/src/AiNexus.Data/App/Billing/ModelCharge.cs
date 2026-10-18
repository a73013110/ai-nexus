using AiNexus.Data.App.Conversations;
using AiNexus.Data.App.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Billing;

public sealed class ModelCharge
{
    // 以呼叫 ID 作為主鍵：重試不會為同一次呼叫建立第二筆費用。
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
    // 輸出 tokens 已包含供應商計費的推理 tokens；推理 tokens 是其子集，不可再另外加總。
    public long? OutputTokens { get; set; }
    public long? ReasoningTokens { get; set; }
    public bool UsageComplete { get; set; }
    public decimal? Amount { get; set; }
    public string State { get; set; } = "pending";
    public string Outcome { get; set; } = "queued";
}

internal sealed class ModelChargeConfiguration : IEntityTypeConfiguration<ModelCharge>
{
    public void Configure(EntityTypeBuilder<ModelCharge> charge)
    {
        charge.ToTable("ModelCharges", "billing");
        charge.HasKey(x => x.Id);
        charge.Property(x => x.Provider).HasMaxLength(32);
        charge.Property(x => x.ModelId).HasMaxLength(160);
        charge.Property(x => x.Operation).HasMaxLength(32);
        charge.Property(x => x.Currency).HasMaxLength(3);
        charge.Property(x => x.Kind).HasMaxLength(16);
        charge.Property(x => x.RequestCharge).HasMaxLength(16);
        charge.Property(x => x.State).HasMaxLength(24);
        charge.Property(x => x.Outcome).HasMaxLength(16);
        charge.HasOne<ModelPrice>().WithMany().HasForeignKey(x => x.PriceId).OnDelete(DeleteBehavior.Restrict);
        charge.Property(x => x.InputPerMillion).HasPrecision(20, 8);
        charge.Property(x => x.CachedInputPerMillion).HasPrecision(20, 8);
        charge.Property(x => x.OutputPerMillion).HasPrecision(20, 8);
        charge.Property(x => x.PerRequest).HasPrecision(20, 8);
        charge.Property(x => x.Amount).HasPrecision(20, 8);
        charge.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        charge.HasOne<Conversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Restrict);
    }
}
