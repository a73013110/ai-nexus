using AiNexus.Data.App.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Organization;

/// <summary>
/// 使用者目前所屬的單位；一人可屬多個單位，最多一個主要所屬（資料庫的 filtered unique index）。
/// 外鍵都是 NO ACTION：刪除單位前要先移除成員；使用者只做軟刪除，所屬保留到管理員移除。
/// </summary>
public sealed class UnitMember
{
    public Guid UnitId { get; set; }
    public Guid UserId { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsManager { get; set; }
}

internal sealed class UnitMemberConfiguration : IEntityTypeConfiguration<UnitMember>
{
    public void Configure(EntityTypeBuilder<UnitMember> member)
    {
        member.ToTable("UnitMembers", "organization");
        member.HasKey(x => new { x.UnitId, x.UserId });
        member.HasOne<Unit>().WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
        member.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
