using AiNexus.Data.App.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Attendance;

/// <summary>每次匯入打卡檔一列（成功或失敗），只增不改；檔案目前狀態在 <see cref="PunchFile"/>。</summary>
public sealed class PunchImport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PunchFileId { get; set; }

    /// <summary><see cref="PunchImportTriggers"/> 之一。</summary>
    public string TriggerKind { get; set; } = "";

    /// <summary>只有手動重匯才可有值；操作者無法辨識時也可為空值。</summary>
    public Guid? RequestedById { get; set; }

    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset FinishedAt { get; set; }

    /// <summary>這次匯入時看到的檔案修改時間；成功時會成為 <see cref="PunchFile.ImportedModifiedAt"/>。</summary>
    public DateTimeOffset SourceModifiedAt { get; set; }

    public long SourceSize { get; set; }

    /// <summary>實際寫入的筆數，扣掉已由其他檔寫入的重複刷卡；失敗時為 0。</summary>
    public int PunchCount { get; set; }

    /// <summary><see cref="PunchImportStatuses"/> 之一。</summary>
    public string Status { get; set; } = "";

    /// <summary>失敗的錯誤訊息；成功時必須為空值。</summary>
    public string? ErrorMessage { get; set; }
}

public static class PunchImportTriggers
{
    public const string Scheduled = "scheduled";
    public const string Manual = "manual";
}

public static class PunchImportStatuses
{
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
}

internal sealed class PunchImportConfiguration : IEntityTypeConfiguration<PunchImport>
{
    public void Configure(EntityTypeBuilder<PunchImport> import)
    {
        import.ToTable("PunchImports", "attendance");
        import.HasKey(x => x.Id);
        import.Property(x => x.TriggerKind).HasMaxLength(16);
        import.Property(x => x.Status).HasMaxLength(16);
        import.Property(x => x.ErrorMessage).HasMaxLength(1000);
        import.HasOne<PunchFile>().WithMany().HasForeignKey(x => x.PunchFileId).OnDelete(DeleteBehavior.Restrict);
        import.HasOne<User>().WithMany().HasForeignKey(x => x.RequestedById).OnDelete(DeleteBehavior.Restrict);
    }
}
