using AiNexus.Data.App.Attendance;

namespace AiNexus.Features.Staff.Attendance.Imports;

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
}
