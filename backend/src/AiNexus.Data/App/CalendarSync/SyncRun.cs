using AiNexus.Data.App.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.CalendarSync;

/// <summary>
/// 日曆同步的一輪：只記有異動、有失敗、被安全閥擋下、無法執行或手動觸發的一輪，只增不改；
/// 沒有異動的排程輪只更新記憶體中的最後檢查時間，免得每五分鐘一列淹沒有用的紀錄。
/// </summary>
public sealed class SyncRun
{
    public const int ErrorMessageMaxLength = 1000;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string CalendarId { get; set; } = "";

    /// <summary><see cref="SyncRunTriggers"/> 之一。</summary>
    public string TriggerKind { get; set; } = "";

    /// <summary>只有手動觸發才有值。</summary>
    public Guid? RequestedById { get; set; }

    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset FinishedAt { get; set; }

    /// <summary><see cref="SyncRunStatuses"/> 之一。</summary>
    public string Status { get; set; } = "";

    /// <summary>從 GD 讀到、應在日曆上的期間數；讀不到 GD 時為 null。</summary>
    public int? SourceCount { get; set; }

    public int CreatedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int DeletedCount { get; set; }
    public int FailedCount { get; set; }

    /// <summary>被安全閥擋下、沒有標為移除的事件數。</summary>
    public int HeldCount { get; set; }

    /// <summary>只有 <see cref="SyncRunStatuses.Held"/>、<see cref="SyncRunStatuses.Failed"/> 才有值。</summary>
    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    /// <summary>這一輪比對的回看天數；設定因站台而異（共用資料庫），紀錄才看得出當時的範圍。明細上線前的紀錄為 null。</summary>
    public int? LookbackDays { get; set; }
}

public static class SyncRunTriggers
{
    public const string Scheduled = "scheduled";
    public const string Manual = "manual";
}

public static class SyncRunStatuses
{
    public const string Succeeded = "succeeded";
    public const string Partial = "partial";
    public const string Held = "held";
    public const string Failed = "failed";
}

internal sealed class SyncRunConfiguration : IEntityTypeConfiguration<SyncRun>
{
    public void Configure(EntityTypeBuilder<SyncRun> run)
    {
        run.ToTable("SyncRuns", "calendarsync");
        run.HasKey(x => x.Id);
        run.Property(x => x.CalendarId).HasMaxLength(CalendarEvent.CalendarIdMaxLength);
        run.Property(x => x.TriggerKind).HasMaxLength(16);
        run.Property(x => x.Status).HasMaxLength(16);
        run.Property(x => x.ErrorCode).HasMaxLength(CalendarEvent.ErrorCodeMaxLength);
        run.Property(x => x.ErrorMessage).HasMaxLength(SyncRun.ErrorMessageMaxLength);
        run.HasOne<User>().WithMany().HasForeignKey(x => x.RequestedById).OnDelete(DeleteBehavior.Restrict);
    }
}
