using AiNexus.Data.App.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.AccessControl;

/// <summary>使用者個人的模型白名單（會再限縮群組授權）與 token 上限覆寫。</summary>
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
        personal.ToTable("UserModelPolicies", "accesscontrol");
        personal.HasKey(x => x.UserId);
        personal.Property(x => x.AllowedModelsJson).HasMaxLength(4000);
        personal.Property(x => x.DailyTokenLimitsJson).HasMaxLength(8000);
        personal.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
