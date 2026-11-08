using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.CalendarSync;

/// <summary>
/// 一輪同步逐筆處理的事件：內容是當下的快照，事件之後再變也不影響這筆紀錄。
/// 主鍵（<see cref="RunId"/>、<see cref="EventId"/>）同時是明細查詢與連帶刪除的路徑；同一輪重試同一列（重設日曆）時只留最後的結果。
/// </summary>
public sealed class SyncRunItem
{
    public Guid RunId { get; set; }

    /// <summary>對應 <see cref="CalendarEvent.Id"/>；不設外鍵，重設日曆會先清事件列。</summary>
    public Guid EventId { get; set; }

    /// <summary><see cref="SyncRunItemOutcomes"/> 之一。</summary>
    public string Outcome { get; set; } = "";

    public string DesNo { get; set; } = "";
    public string Kind { get; set; } = "";
    public string ApplicantName { get; set; } = "";
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }

    /// <summary><see cref="CalendarEventCauses"/> 之一；新事件為 null。</summary>
    public string? Cause { get; set; }

    public string? ChangesJson { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>事件此刻的快照；原因與錯誤取自事件列，呼叫端要在清掉它們之前取。</summary>
    public static SyncRunItem Snapshot(CalendarEvent row, string outcome) => new()
    {
        EventId = row.Id,
        Outcome = outcome,
        DesNo = row.DesNo,
        Kind = row.Kind,
        ApplicantName = row.ApplicantName,
        StartsAt = row.StartsAt,
        EndsAt = row.EndsAt,
        Cause = row.PendingCause,
        ChangesJson = row.PendingChangesJson,
        ErrorCode = row.LastErrorCode,
        ErrorMessage = row.LastError,
    };
}

public static class SyncRunItemOutcomes
{
    public const string Created = "created";
    public const string Updated = "updated";
    public const string Deleted = "deleted";
    public const string Failed = "failed";
    public const string Held = "held";
}

internal sealed class SyncRunItemConfiguration : IEntityTypeConfiguration<SyncRunItem>
{
    public void Configure(EntityTypeBuilder<SyncRunItem> item)
    {
        item.ToTable("SyncRunItems", "calendarsync");
        item.HasKey(x => new { x.RunId, x.EventId });
        item.Property(x => x.Outcome).HasMaxLength(16);
        item.Property(x => x.DesNo).HasMaxLength(CalendarEvent.DesNoMaxLength);
        item.Property(x => x.Kind).HasMaxLength(16);
        item.Property(x => x.ApplicantName).HasMaxLength(CalendarEvent.NameMaxLength);
        item.Property(x => x.Cause).HasMaxLength(CalendarEvent.CauseMaxLength);
        item.Property(x => x.ChangesJson).HasMaxLength(CalendarEvent.ChangesMaxLength);
        item.Property(x => x.ErrorCode).HasMaxLength(CalendarEvent.ErrorCodeMaxLength);
        item.Property(x => x.ErrorMessage).HasMaxLength(CalendarEvent.ErrorMaxLength);
        item.HasOne<SyncRun>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Cascade);
    }
}
