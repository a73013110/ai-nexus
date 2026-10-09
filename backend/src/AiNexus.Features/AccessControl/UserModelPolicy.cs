using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.AccessControl;

/// <summary>A user's personal whitelist (narrows group grants) and token cap overrides.</summary>
public sealed class UserModelPolicy
{
    public Guid UserId { get; set; }
    public string? AllowedModelsJson { get; set; }
    public string? DailyTokenLimitsJson { get; set; }
}

internal sealed class UserModelPolicyConfiguration : IEntityTypeConfiguration<UserModelPolicy>
{
    public void Configure(EntityTypeBuilder<UserModelPolicy> personal)
    {
        personal.ToTable("UserModelPolicies", "access"); personal.HasKey(x => x.UserId);
        personal.Property(x => x.AllowedModelsJson).HasMaxLength(4000); personal.Property(x => x.DailyTokenLimitsJson).HasMaxLength(8000);
    }
}
