using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Office.Attendance.Imports;

/// <summary>檔案列與它最近一次的匯入（成功或失敗）；排程判斷要不要重匯只看這兩者。</summary>
public sealed record PunchFileState(PunchFile File, PunchImport? Last);

/// <summary>排程判斷一個檔案要不要重匯的規則；掃描時先篩一次，真正匯入前在互斥鎖內以最新狀態再判斷一次。</summary>
public static class PunchImportPlanner
{
    /// <summary>
    /// 從沒匯入過、版本（修改時間或大小）與上次成功匯入的不同、或被標記為要重匯（版本欄位被清成空值）都要匯入。
    /// 最近一次匯入失敗的檔案也要重匯，但檔案沒有再變動時要等 <paramref name="failedRetryAfter"/>，避免壞檔每個掃描週期都產生一筆失敗紀錄；
    /// 檔案相對於失敗的那次又有變動，就不等。
    /// </summary>
    public static bool NeedsImport(PunchFile? file, PunchImport? lastImport, DateTimeOffset modifiedAt, long size, DateTimeOffset now, TimeSpan failedRetryAfter)
    {
        if (file is null) return true;
        if (lastImport is { Status: PunchImportStatuses.Failed })
        {
            var unchangedSinceFailure = lastImport.SourceModifiedAt == modifiedAt && lastImport.SourceSize == size;
            return !unchangedSinceFailure || now - lastImport.FinishedAt >= failedRetryAfter;
        }
        return file.ImportedModifiedAt != modifiedAt || file.ImportedSize != size;
    }

    /// <summary>
    /// 最後修改時間距今不到 <paramref name="settle"/> 的檔案先不處理（可能還在寫入）。
    /// 修改時間比現在還晚（共用主機時鐘比較快）不算，否則時鐘差多少就永遠等多久。
    /// </summary>
    public static bool IsSettling(DateTimeOffset modifiedAt, DateTimeOffset now, TimeSpan settle)
        => settle > TimeSpan.Zero && now >= modifiedAt && now - modifiedAt < settle;

    internal static bool NeedsImport(PunchFileState? state, FileInfo info, DateTimeOffset now, PunchImportOptions settings)
        => NeedsImport(state?.File, state?.Last, ModifiedAt(info), info.Length, now, TimeSpan.FromSeconds(settings.FailedRetrySeconds));

    internal static bool IsSettling(FileInfo info, DateTimeOffset now, PunchImportOptions settings)
        => IsSettling(ModifiedAt(info), now, TimeSpan.FromSeconds(settings.SettleSeconds));

    /// <summary>檔案版本的修改時間；與 <see cref="PunchFile.ImportedModifiedAt"/> 比較，兩邊都是 UTC、100 ns 精度。</summary>
    internal static DateTimeOffset ModifiedAt(FileInfo info) => new(info.LastWriteTimeUtc, TimeSpan.Zero);

    /// <summary>
    /// 檔案列連同最近一次匯入；<paramref name="fileName"/> 為 null 時是全部檔案（掃描用）。
    /// 篩選放在投影之前：投影成 <see cref="PunchFileState"/> 之後 EF 無法再翻譯它的成員。
    /// </summary>
    internal static IQueryable<PunchFileState> States(AppDbContext db, string? fileName = null)
    {
        var files = db.Set<PunchFile>().AsNoTracking();
        if (fileName is not null) files = files.Where(x => x.FileName == fileName);
        return from file in files
               join import in db.Set<PunchImport>().AsNoTracking() on file.LastImportId equals import.Id into imports
               from last in imports.DefaultIfEmpty()
               select new PunchFileState(file, last);
    }
}
