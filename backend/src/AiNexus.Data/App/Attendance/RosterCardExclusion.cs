using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Attendance;

/// <summary>
/// HR 依卡號標記為不列入出勤：沒有帳號的資料（只有姓名的持有期間、未歸戶刷卡）依附在卡號上，同名同姓也不會被一起隱藏。
/// 有列＝不列入；只影響沒有帳號的資料，使用者另由 <see cref="RosterExclusion"/> 標記。卡號不關聯 <see cref="Card"/>，未登錄的卡號也能標。
/// </summary>
public sealed class RosterCardExclusion
{
    public string CardNumber { get; set; } = "";
    public string? Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class RosterCardExclusionConfiguration : IEntityTypeConfiguration<RosterCardExclusion>
{
    public void Configure(EntityTypeBuilder<RosterCardExclusion> exclusion)
    {
        exclusion.Property(x => x.CreatedAt).HasValueGenerator<CreationTime>();
        exclusion.ToTable("RosterCardExclusions", "attendance");
        exclusion.HasKey(x => x.CardNumber);
        exclusion.Property(x => x.CardNumber).HasMaxLength(Card.CardNumberMaxLength);
        exclusion.Property(x => x.Note).HasMaxLength(RosterExclusion.NoteMaxLength);
    }
}
