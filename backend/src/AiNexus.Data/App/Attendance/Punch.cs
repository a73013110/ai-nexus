using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Attendance;

/// <summary>
/// 打卡機的原始刷卡，每行一筆。刻意不對 <see cref="Card"/> 設外鍵：打卡檔可能出現尚未登錄的卡號，匯入不能因此失敗；
/// 持有人由 <see cref="AttributedPunch"/> 在讀取時依持有期間計算。
/// </summary>
public sealed class Punch
{
    /// <summary>機器代號與事件代號的長度上限。</summary>
    public const int CodeMaxLength = 16;

    /// <summary>自動遞增；量大不用 GUID。</summary>
    public long Id { get; set; }

    public Guid PunchFileId { get; set; }
    public string CardNumber { get; set; } = "";

    /// <summary>打卡機的台灣本地時間，固定 +08:00（資料庫以 check 限制）。</summary>
    public DateTimeOffset PunchedAt { get; set; }

    /// <summary>依刷卡時間（不是檔名）取的本地日期，必須等於 <see cref="PunchedAt"/> 的日期。</summary>
    public DateOnly AttendanceDate { get; set; }

    /// <summary>原樣保存、不解讀，是去重鍵的一部分。</summary>
    public string MachineCode { get; set; } = "";

    /// <summary>原樣保存、不解讀。</summary>
    public string EventCode { get; set; } = "";
}

internal sealed class PunchConfiguration : IEntityTypeConfiguration<Punch>
{
    public void Configure(EntityTypeBuilder<Punch> punch)
    {
        punch.ToTable("Punches", "attendance");
        punch.HasKey(x => x.Id);
        punch.Property(x => x.Id).UseIdentityColumn();
        punch.Property(x => x.CardNumber).HasMaxLength(Card.CardNumberMaxLength);
        punch.Property(x => x.MachineCode).HasMaxLength(Punch.CodeMaxLength);
        punch.Property(x => x.EventCode).HasMaxLength(Punch.CodeMaxLength);
        punch.HasOne<PunchFile>().WithMany().HasForeignKey(x => x.PunchFileId).OnDelete(DeleteBehavior.Restrict);
    }
}
