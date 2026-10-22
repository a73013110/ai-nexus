using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Data.App.Attendance;

/// <summary>
/// 打卡機輸出的每日檔（yyyyMMdd.txt）目前的狀態，一個檔一列，用來判斷要不要重匯並串起 <see cref="Punch"/>；
/// 逐次匯入的歷史在 <see cref="PunchImport"/>。
/// </summary>
public sealed class PunchFile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>不含路徑；檔名日期只是輸出日，不是出勤日。</summary>
    public string FileName { get; set; } = "";

    /// <summary>上次成功匯入時檔案的修改時間；從未成功匯入為空值（視為要匯入）。與 <see cref="ImportedSize"/> 同時有值或同時為空。</summary>
    public DateTimeOffset? ImportedModifiedAt { get; set; }

    public long? ImportedSize { get; set; }

    /// <summary>最近一次匯入（成功或失敗）；最近一次失敗也要重匯，所以判斷時要看它的狀態。尚未寫入匯入紀錄時為空值。</summary>
    public Guid? LastImportId { get; set; }
}

internal sealed class PunchFileConfiguration : IEntityTypeConfiguration<PunchFile>
{
    public void Configure(EntityTypeBuilder<PunchFile> file)
    {
        file.ToTable("PunchFiles", "attendance");
        file.HasKey(x => x.Id);
        file.Property(x => x.FileName).HasMaxLength(128);
        // 與 PunchImport.PunchFileId 互相參照；兩邊都明確指定外鍵欄位且不設導覽屬性，EF 才不會把兩條外鍵當成同一個關聯的兩端。
        file.HasOne<PunchImport>().WithMany().HasForeignKey(x => x.LastImportId).OnDelete(DeleteBehavior.Restrict);
    }
}
