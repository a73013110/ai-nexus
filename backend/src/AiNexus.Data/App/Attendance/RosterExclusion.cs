using AiNexus.Data.App.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Attendance;

/// <summary>
/// HR 標記為不列入出勤的使用者：有列＝不列入，沒有列＝預設列入。
/// 只是 HR 看全公司時的檢視篩選，不是權限；本人與主管仍依資料範圍看得到。
/// </summary>
public sealed class RosterExclusion
{
    public Guid UserId { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class RosterExclusionConfiguration : IEntityTypeConfiguration<RosterExclusion>
{
    public void Configure(EntityTypeBuilder<RosterExclusion> exclusion)
    {
        exclusion.Property(x => x.CreatedAt).HasValueGenerator<CreationTime>();
        exclusion.ToTable("RosterExclusions", "attendance");
        exclusion.HasKey(x => x.UserId);
        exclusion.Property(x => x.Note).HasMaxLength(200);
        exclusion.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
