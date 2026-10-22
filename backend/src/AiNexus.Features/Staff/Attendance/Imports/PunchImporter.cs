using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Staff.Attendance.Imports;

/// <summary>
/// 以檔案為單位的刷卡匯入（ADR 0018）：每日檔是當天的快照，重匯時這個檔先前寫入的列要換成新內容。
/// <para>
/// <b>交易</b>：解析在交易外（讀不到或格式錯誤都不碰資料庫），之後在單一交易內寫刷卡的增刪與匯入列，再讓檔案列指向它。
/// 任何錯誤整個回復，<c>Punches</c> 維持上次成功的內容；失敗的匯入列另外在回復之後的新交易寫入，否則會跟著回復而消失。
/// </para>
/// <para>
/// <b>去重</b>：刷卡的唯一鍵是（卡號、時間、機器代號）。新內容裡已屬於別的檔的那一筆不重寫，歸先寫入的檔。
/// </para>
/// <para>
/// <b>鄰檔補救</b>：同一筆刷卡出現在相鄰兩天的檔（前一天深夜的刷卡也會出現在次日的檔）時，它屬於先匯入的 A 檔，B 檔那份被當成重複略過。
/// 之後重匯 A、而新內容已沒有這一筆，刪掉它就會讓資料憑空消失，因為 B 檔其實還有。所以刪除刷卡時，在同一個交易內把可能含有它的其他檔
/// （檔名日期為該刷卡出勤日的前一天、當天與後一天）標記為要重匯（清掉 <c>ImportedModifiedAt</c> 與 <c>ImportedSize</c>），排程下一輪就會重匯它們，
/// 這筆刷卡便改歸 B。標記與刪除同交易，所以不會有「已刪除但還沒標記」的瞬間，程序在中間中止也不會遺失。
/// </para>
/// 一次只處理一個匯入（<see cref="PunchImportGate"/>）。
/// </summary>
public sealed partial class PunchImporter(
    AppDbContext db,
    PunchImportGate gate,
    IOptions<AttendanceOptions> options,
    ILogger<PunchImporter> logger,
    TimeProvider clock)
{
    /// <summary>HR 手動重匯：不論檔案有沒有變動都重匯。找不到檔案或來源資料夾讀不到時回傳錯誤，不留匯入紀錄（沒有檔案可記）；匯入本身失敗則回傳狀態為 failed 的匯入列。</summary>
    public async Task<Result<PunchImport>> ReimportAsync(string fileName, Guid requestedBy, CancellationToken ct)
    {
        await gate.Gate.WaitAsync(ct);
        try
        {
            var located = await Task.Run(() => Locate(fileName), ct);
            if (!located.IsSuccess) return located.Error;
            return await ImportAsync(located.Value, PunchImportTriggers.Manual, requestedBy, ct);
        }
        finally
        {
            gate.Gate.Release();
        }
    }

    /// <summary>
    /// 排程匯入：進入互斥鎖後以最新的檔案與資料庫狀態重新判斷，等鎖期間別的匯入可能已經處理過。不需要匯入或檔案已不存在回傳 null。
    /// </summary>
    public async Task<PunchImport?> ImportIfChangedAsync(string fileName, CancellationToken ct)
    {
        await gate.Gate.WaitAsync(ct);
        try
        {
            var located = await Task.Run(() => Locate(fileName), ct);
            if (!located.IsSuccess) return null;
            var current = await PunchImportPlanner.States(db, fileName).SingleOrDefaultAsync(ct);
            if (!PunchImportPlanner.NeedsImport(current, located.Value, clock.GetUtcNow(), options.Value.Import)) return null;
            return await ImportAsync(located.Value, PunchImportTriggers.Scheduled, null, ct);
        }
        finally
        {
            gate.Gate.Release();
        }
    }

    private Result<FileInfo> Locate(string fileName)
    {
        try
        {
            var directory = new DirectoryInfo(options.Value.Import.SourceDirectory);
            if (!directory.Exists) return AttendanceErrors.ImportSourceUnavailable;
            var info = new FileInfo(Path.Combine(directory.FullName, fileName));
            return info.Exists ? info : AttendanceErrors.ImportFileNotFound;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return AttendanceErrors.ImportSourceUnavailable;
        }
    }

    private async Task<PunchImport> ImportAsync(FileInfo info, string trigger, Guid? requester, CancellationToken ct)
    {
        // 版本在讀檔之前取：讀的途中檔案又被寫入，記下的是較舊的版本，下一輪掃描會發現不同而重匯；反過來才會漏掉。
        var attempt = new Attempt(info.Name, PunchImportPlanner.ModifiedAt(info), info.Length, trigger, requester, clock.GetUtcNow());
        string failure;
        string reason;
        try
        {
            var punches = PunchFileParser.Parse(PunchFileParser.Decode(await ReadAsync(info, ct)));
            try
            {
                var (import, requeued) = await CommitAsync(attempt, punches, ct);
                LogImported(logger, trigger, import.PunchCount, import.PunchFileId);
                if (requeued > 0) LogRequeued(logger, requeued, import.PunchFileId);
                return import;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // 例外訊息可能含 SQL 與資料值，不寫進給 HR 看的紀錄；型別與 SQL 錯誤碼可在日誌診斷頁查證。
                (failure, reason) = ($"寫入資料庫失敗（{ex.GetType().Name}），這次匯入已整個回復。", "write");
            }
        }
        catch (PunchFileFormatException ex)
        {
            (failure, reason) = (ex.Message, "format");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            (failure, reason) = ($"無法讀取來源檔（{ex.GetType().Name}）。", "read");
        }
        var failed = await RecordFailureAsync(attempt, failure, ct);
        LogFailed(logger, trigger, reason, failed.PunchFileId);
        return failed;
    }

    /// <summary>以允許寫入端同時開著的方式讀取：打卡機輸出時也不會被我們擋住，反過來也不鎖住打卡機的寫入。</summary>
    private async Task<byte[]> ReadAsync(FileInfo info, CancellationToken ct)
    {
        var limit = options.Value.Import.MaxFileBytes;
        if (info.Length > limit) throw TooLarge(limit);
        await using var stream = new FileStream(info.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, useAsync: true);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        // 上面的檢查用的是 stat 的結果；stat 之後檔案又長大時，讀完再檢查一次。
        if (buffer.Length > limit) throw TooLarge(limit);
        return buffer.ToArray();
    }

    private static PunchFileFormatException TooLarge(int limit)
        => new(limit >= 1 << 20 ? $"檔案超過 {limit / (double)(1 << 20):0.#} MB 的上限。" : $"檔案超過 {limit / 1024} KB 的上限。");

    private async Task<(PunchImport Import, int Requeued)> CommitAsync(Attempt attempt, IReadOnlyList<ParsedPunch> punches, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var file = await FileRowAsync(attempt.FileName, ct);

        // 只載入新內容涵蓋的出勤日、屬於別的檔的刷卡：同一筆刷卡的出勤日必定相同，範圍之外不可能重複。
        var foreign = new HashSet<(string, long, string)>();
        if (punches.Count > 0)
        {
            var (first, last) = (punches.Min(x => x.AttendanceDate), punches.Max(x => x.AttendanceDate));
            var rows = await db.Set<Punch>().AsNoTracking().Where(x => x.PunchFileId != file.Id && x.AttendanceDate >= first && x.AttendanceDate <= last)
                .Select(x => new { x.CardNumber, x.PunchedAt, x.MachineCode }).ToListAsync(ct);
            foreign = [.. rows.Select(x => ParsedPunch.KeyOf(x.CardNumber, x.PunchedAt, x.MachineCode))];
        }

        // 結果與「刪光再寫入新內容」相同，但只動有差異的列：寫入量小，不變的列也保留原本的 Id。
        var incoming = punches.ToDictionary(x => x.Key);
        var owned = await db.Set<Punch>().Where(x => x.PunchFileId == file.Id).ToListAsync(ct);
        var dropped = new List<Punch>();
        foreach (var row in owned)
        {
            if (incoming.Remove(ParsedPunch.KeyOf(row.CardNumber, row.PunchedAt, row.MachineCode), out var punch)) row.EventCode = punch.EventCode;
            else dropped.Add(row);
        }
        var added = incoming.Values.Where(x => !foreign.Contains(x.Key)).Select(x => new Punch
        {
            PunchFileId = file.Id,
            CardNumber = x.CardNumber,
            PunchedAt = x.PunchedAt,
            AttendanceDate = x.AttendanceDate,
            MachineCode = x.MachineCode,
            EventCode = x.EventCode,
        }).ToList();
        db.RemoveRange(dropped);
        db.AddRange(added);
        var requeued = await RequeueNeighboursAsync(file, dropped, ct);

        var import = await RecordAsync(file, attempt, owned.Count - dropped.Count + added.Count, null, ct);
        await transaction.CommitAsync(ct);
        return (import, requeued);
    }

    /// <summary>
    /// 被刪除的刷卡可能還存在別的檔（見類別說明）：把可能含有它們的、已經成功匯入過的檔標記為要重匯。
    /// 從沒成功匯入的檔（版本本來就是空值）下一輪也會匯入，不必標記。
    /// </summary>
    private async Task<int> RequeueNeighboursAsync(PunchFile file, List<Punch> dropped, CancellationToken ct)
    {
        if (dropped.Count == 0) return 0;
        var names = dropped.Select(x => x.AttendanceDate).Distinct()
            .SelectMany(date => new[] { date.AddDays(-1), date, date.AddDays(1) }).Select(PunchFileParser.FileNameFor).Distinct().ToList();
        return await db.Set<PunchFile>().Where(x => names.Contains(x.FileName) && x.Id != file.Id && x.ImportedModifiedAt != null)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.ImportedModifiedAt, (DateTimeOffset?)null).SetProperty(x => x.ImportedSize, (long?)null), ct);
    }

    /// <summary>在回復之後的新交易寫入失敗的匯入列；檔案列的版本不動（<c>Punches</c> 仍是上次成功匯入的內容），只更新 <c>LastImportId</c>。</summary>
    private async Task<PunchImport> RecordFailureAsync(Attempt attempt, string message, CancellationToken ct)
    {
        // 失敗的交易留下的追蹤狀態不能再用。
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var file = await FileRowAsync(attempt.FileName, ct);
        var import = await RecordAsync(file, attempt, 0, message.Length > PunchImport.ErrorMessageMaxLength ? message[..PunchImport.ErrorMessageMaxLength] : message, ct);
        await transaction.CommitAsync(ct);
        return import;
    }

    private async Task<PunchFile> FileRowAsync(string fileName, CancellationToken ct)
    {
        var file = await db.Set<PunchFile>().SingleOrDefaultAsync(x => x.FileName == fileName, ct);
        if (file is not null) return file;
        file = new() { FileName = fileName };
        db.Add(file);
        await db.SaveChangesAsync(ct);
        return file;
    }

    /// <summary>
    /// 寫入匯入列（<paramref name="error"/> 為 null 是成功）並讓檔案列指向它；成功時檔案列的版本也換成這次的。
    /// 兩張表互相參照（<c>PunchImports.PunchFileId</c>、<c>PunchFiles.LastImportId</c>），所以匯入列先存、檔案列後改。
    /// 交易中追蹤的刷卡增刪也在第一次儲存時一起送出。
    /// </summary>
    private async Task<PunchImport> RecordAsync(PunchFile file, Attempt attempt, int punchCount, string? error, CancellationToken ct)
    {
        var import = new PunchImport
        {
            PunchFileId = file.Id,
            TriggerKind = attempt.Trigger,
            RequestedById = attempt.Requester,
            StartedAt = attempt.StartedAt,
            FinishedAt = Later(attempt.StartedAt),
            SourceModifiedAt = attempt.ModifiedAt,
            SourceSize = attempt.Size,
            PunchCount = punchCount,
            Status = error is null ? PunchImportStatuses.Succeeded : PunchImportStatuses.Failed,
            ErrorMessage = error,
        };
        db.Add(import);
        await db.SaveChangesAsync(ct);
        file.LastImportId = import.Id;
        if (error is null) (file.ImportedModifiedAt, file.ImportedSize) = (attempt.ModifiedAt, attempt.Size);
        await db.SaveChangesAsync(ct);
        return import;
    }

    /// <summary>牆上時鐘可能被校時往回調；資料庫限制結束時間不早於開始時間。</summary>
    private DateTimeOffset Later(DateTimeOffset started) => clock.GetUtcNow() is var now && now > started ? now : started;

    /// <summary>一次匯入的來源版本與觸發資訊；<see cref="Requester"/> 只有手動重匯才有值。</summary>
    private sealed record Attempt(string FileName, DateTimeOffset ModifiedAt, long Size, string Trigger, Guid? Requester, DateTimeOffset StartedAt);

    [LoggerMessage(EventId = 3400, EventName = "attendance.import_succeeded", Level = LogLevel.Information, Message = "Punch file imported ({Kind}): {Count} punches, file {ResourceId}.")]
    private static partial void LogImported(ILogger logger, string kind, int count, Guid resourceId);

    [LoggerMessage(EventId = 3401, EventName = "attendance.import_failed", Level = LogLevel.Warning, Message = "Punch file import failed ({Kind}, {Reason}), file {ResourceId}; punches keep the last successful content.")]
    private static partial void LogFailed(ILogger logger, string kind, string reason, Guid resourceId);

    [LoggerMessage(EventId = 3402, EventName = "attendance.import_requeued", Level = LogLevel.Information, Message = "Queued {Count} neighbouring punch files for re-import after punches left file {ResourceId}.")]
    private static partial void LogRequeued(ILogger logger, int count, Guid resourceId);
}
