using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.CalendarSync;

/// <summary>
/// 公文系統的請假單、出差單在 Google 日曆上的一段期間：一般表單一列，跨區間假單每段一列。
/// 期望內容（<see cref="ContentHash"/>）每輪由 GD 重算，與上次成功推送的內容（<see cref="SyncedHash"/>）不同就待同步；
/// 不該再出現在日曆時標上 <see cref="RemovedAt"/>，待同步的動作變成刪除，列保留供查證。
/// </summary>
public sealed class CalendarEvent
{
    public const int CalendarIdMaxLength = 200;
    public const int DesNoMaxLength = 20;
    public const int SegmentKeyMaxLength = 40;
    public const int NameMaxLength = 50;
    public const int GoogleEventIdMaxLength = 1024;
    public const int HtmlLinkMaxLength = 1000;
    public const int ErrorCodeMaxLength = 64;
    public const int ErrorMaxLength = 500;
    public const int HashLength = 32;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string CalendarId { get; set; } = "";
    public string DesNo { get; set; } = "";

    /// <summary>一般表單為空字串；跨區間假單是該段開始日期 yyyyMMdd 加上 GD 的 DayOfWeek（與舊系統相同）。</summary>
    public string SegmentKey { get; set; } = "";

    /// <summary><see cref="CalendarEventKinds"/> 之一。</summary>
    public string Kind { get; set; } = "";

    public string ApplicantName { get; set; } = "";
    public string? AgentName { get; set; }

    /// <summary>最後一次看到的公文狀態（GD FlowOver：Send、Decide）；移除後保留移除前的值。</summary>
    public string FlowStatus { get; set; } = "";

    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }

    /// <summary>期望的 Google 事件內容的 SHA-256。</summary>
    public byte[] ContentHash { get; set; } = [];

    /// <summary>新列建立時就決定（等於 <see cref="Id"/> 的十六進位字串），舊系統轉入的列是 Google 產生的 ID。</summary>
    public string GoogleEventId { get; set; } = "";

    /// <summary>最後一次成功推送的 <see cref="ContentHash"/>；null 表示還沒成功建立過。</summary>
    public byte[]? SyncedHash { get; set; }

    public string? HtmlLink { get; set; }

    /// <summary><see cref="CalendarEventStates"/> 之一。</summary>
    public string State { get; set; } = "";

    public DateTimeOffset? RemovedAt { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }

    /// <summary>只有 <see cref="CalendarEventStates.Failed"/> 才有值。</summary>
    public string? LastErrorCode { get; set; }

    /// <summary>只有 <see cref="CalendarEventStates.Failed"/> 才有值。</summary>
    public string? LastError { get; set; }

    public DateTimeOffset? SyncedAt { get; set; }

    /// <summary>舊系統 EDocSyncToGoogleCalendar.UUID，只有轉入的列有值。</summary>
    public string? LegacyUuid { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>新列的 Google event id：主鍵的 32 位小寫十六進位字串，落在 Google 允許的字元（a–v、0–9）內。</summary>
    public static string NewGoogleEventId(Guid id) => id.ToString("N");
}

public static class CalendarEventKinds
{
    public const string Leave = "leave";
    public const string Travel = "travel";
}

public static class CalendarEventStates
{
    public const string Pending = "pending";
    public const string Failed = "failed";
    public const string Synced = "synced";
    public const string Removed = "removed";
}

internal sealed class CalendarEventConfiguration : IEntityTypeConfiguration<CalendarEvent>
{
    public void Configure(EntityTypeBuilder<CalendarEvent> calendarEvent)
    {
        calendarEvent.ToTable("CalendarEvents", "calendarsync");
        calendarEvent.HasKey(x => x.Id);
        calendarEvent.Property(x => x.CalendarId).HasMaxLength(CalendarEvent.CalendarIdMaxLength);
        calendarEvent.Property(x => x.DesNo).HasMaxLength(CalendarEvent.DesNoMaxLength);
        calendarEvent.Property(x => x.SegmentKey).HasMaxLength(CalendarEvent.SegmentKeyMaxLength);
        calendarEvent.Property(x => x.Kind).HasMaxLength(16);
        calendarEvent.Property(x => x.ApplicantName).HasMaxLength(CalendarEvent.NameMaxLength);
        calendarEvent.Property(x => x.AgentName).HasMaxLength(CalendarEvent.NameMaxLength);
        calendarEvent.Property(x => x.FlowStatus).HasMaxLength(16);
        calendarEvent.Property(x => x.ContentHash).HasMaxLength(CalendarEvent.HashLength).IsFixedLength();
        calendarEvent.Property(x => x.SyncedHash).HasMaxLength(CalendarEvent.HashLength).IsFixedLength();
        calendarEvent.Property(x => x.GoogleEventId).HasMaxLength(CalendarEvent.GoogleEventIdMaxLength);
        calendarEvent.Property(x => x.HtmlLink).HasMaxLength(CalendarEvent.HtmlLinkMaxLength);
        calendarEvent.Property(x => x.State).HasMaxLength(16);
        calendarEvent.Property(x => x.LastErrorCode).HasMaxLength(CalendarEvent.ErrorCodeMaxLength);
        calendarEvent.Property(x => x.LastError).HasMaxLength(CalendarEvent.ErrorMaxLength);
        calendarEvent.Property(x => x.LegacyUuid).HasMaxLength(200);
    }
}
