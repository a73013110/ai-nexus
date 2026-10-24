using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Attendance;

/// <summary>
/// <c>attendance.vw_AttributedPunches</c> 的一列：每筆刷卡依持有期間歸戶到持有人，只讀。
/// 對不到持有期間的刷卡也會出現（<see cref="AssignmentId"/>、<see cref="UserId"/>、<see cref="HolderName"/> 為 null）；
/// 只有 <see cref="HolderName"/> 沒有 <see cref="UserId"/> 的列是已歸戶（持有人還沒有帳號）。
/// 同一卡片的期間重疊會讓同一筆刷卡重複出現，所以 <see cref="PunchId"/> 不是唯一鍵。
/// </summary>
public sealed class AttributedPunch
{
    public long PunchId { get; init; }
    public Guid PunchFileId { get; init; }
    public string CardNumber { get; init; } = "";
    public DateTimeOffset PunchedAt { get; init; }
    public DateOnly AttendanceDate { get; init; }
    public string MachineCode { get; init; } = "";
    public string EventCode { get; init; } = "";

    /// <summary><see cref="Card"/> 沒有這個卡號時為 null（匯入會補建，正常不會發生）。</summary>
    public Guid? CardId { get; init; }

    public Guid? AssignmentId { get; init; }
    public Guid? UserId { get; init; }
    public string? HolderName { get; init; }

    /// <summary>沒有持有期間涵蓋；<see cref="Card"/> 沒有這個卡號也算。view 以 <c>ISNULL</c> 保證 NOT NULL。</summary>
    public bool IsUnattributed { get; init; }
}

internal sealed class AttributedPunchConfiguration : IEntityTypeConfiguration<AttributedPunch>
{
    public void Configure(EntityTypeBuilder<AttributedPunch> punch)
    {
        // 期間重疊時同一筆刷卡會出現多列，沒有唯一鍵，所以是 keyless；檢視只讀，不能追蹤也不能寫入。
        punch.HasNoKey().ToView("vw_AttributedPunches", "attendance");
        punch.Property(x => x.CardNumber).HasMaxLength(Card.CardNumberMaxLength);
        punch.Property(x => x.MachineCode).HasMaxLength(Punch.CodeMaxLength);
        punch.Property(x => x.EventCode).HasMaxLength(Punch.CodeMaxLength);
        punch.Property(x => x.HolderName).HasMaxLength(CardAssignment.HolderNameMaxLength);
    }
}
