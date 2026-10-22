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

    /// <summary>卡號沒有登錄在 <see cref="Card"/> 時為 null。</summary>
    public Guid? CardId { get; init; }

    public string? CardKind { get; init; }
    public Guid? AssignmentId { get; init; }
    public Guid? UserId { get; init; }
    public string? HolderName { get; init; }

    /// <summary>沒有持有期間涵蓋且不是訪客卡；沒登錄的卡號也算。view 以 <c>ISNULL</c> 保證 NOT NULL。</summary>
    public bool IsUnattributed { get; init; }
}

internal sealed class AttributedPunchConfiguration : IEntityTypeConfiguration<AttributedPunch>
{
    public void Configure(EntityTypeBuilder<AttributedPunch> punch)
    {
        // 期間重疊時同一筆刷卡會出現多列，沒有唯一鍵，所以是 keyless；檢視只讀，不能追蹤也不能寫入。
        punch.HasNoKey().ToView("vw_AttributedPunches", "attendance");
        punch.Property(x => x.CardNumber).HasMaxLength(Card.CardNumberMaxLength);
        punch.Property(x => x.MachineCode).HasMaxLength(16);
        punch.Property(x => x.EventCode).HasMaxLength(16);
        punch.Property(x => x.CardKind).HasMaxLength(16);
        punch.Property(x => x.HolderName).HasMaxLength(CardAssignment.HolderNameMaxLength);
    }
}
