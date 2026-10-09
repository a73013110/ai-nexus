using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.AccessControl;

/// <summary>A user's personal whitelist (narrows group grants) and token cap overrides.</summary>
[Comment("使用者的模型白名單與各模型每日 token 覆寫政策。")]
public sealed class UserModelPolicy
{
    [Comment("關聯使用者的 Users 主鍵。")]
    public Guid UserId { get; set; }
    [Comment("模型白名單 JSON；群組取聯集，空值授予全部、空陣列不授權；個人白名單再限縮。")]
    public string? AllowedModelsJson { get; set; }
    [Comment("各模型每日輸入加輸出 token 上限 JSON；授權群組取最高值、留空不限，個人覆寫優先，UTC 午夜重設。")]
    public string? DailyTokenLimitsJson { get; set; }
}

internal sealed class UserModelPolicyConfiguration : IEntityTypeConfiguration<UserModelPolicy>
{
    public void Configure(EntityTypeBuilder<UserModelPolicy> personal)
    {
        personal.ToTable("UserModelPolicies", "accesscontrol"); personal.HasKey(x => x.UserId);
        personal.Property(x => x.AllowedModelsJson).HasMaxLength(4000); personal.Property(x => x.DailyTokenLimitsJson).HasMaxLength(8000);
    }
}
