using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.AccessControl;

/// <summary>A group's model whitelist, daily token caps and attachment capacity. Groups grant additively.</summary>
[Comment("功能群組的模型白名單、各模型每日 token 及附件空間限制。")]
public sealed class GroupModelPolicy
{
    [Comment("關聯功能群組的識別碼。")]
    public string GroupId { get; set; } = "";
    [Comment("模型白名單 JSON；群組取聯集，空值授予全部、空陣列不授權；個人白名單再限縮。")]
    public string? AllowedModelsJson { get; set; }
    [Comment("各模型每日輸入加輸出 token 上限 JSON；授權群組取最高值、留空不限，個人覆寫優先，UTC 午夜重設。")]
    public string? DailyTokenLimitsJson { get; set; }
    [Comment("個人附件儲存上限，以 bytes 計；群組限制取最低值。")]
    public long? StoredAttachmentLimitBytes { get; set; }
}

internal sealed class GroupModelPolicyConfiguration : IEntityTypeConfiguration<GroupModelPolicy>
{
    public void Configure(EntityTypeBuilder<GroupModelPolicy> policy)
    {
        policy.ToTable("GroupModelPolicies", "accesscontrol"); policy.HasKey(x => x.GroupId);
        policy.Property(x => x.GroupId).HasMaxLength(64); policy.Property(x => x.AllowedModelsJson).HasMaxLength(4000); policy.Property(x => x.DailyTokenLimitsJson).HasMaxLength(8000);
        policy.HasOne<RoleGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Cascade);
    }
}
