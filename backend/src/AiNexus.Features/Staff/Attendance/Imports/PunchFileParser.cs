using System.Globalization;
using System.Text;

namespace AiNexus.Features.Staff.Attendance.Imports;

/// <summary>
/// 打卡機每日檔的解析。每行 <c>機器代號:卡號:yyyyMMdd:HHmmss:事件代號</c>，第一欄與最後一欄原樣保存、不解讀。
/// 格式錯誤的行讓整個檔案解析失敗（<see cref="PunchFileFormatException"/>），不略過：匯入紀錄沒有「略過筆數」可記，
/// 靜默少掉一筆刷卡會變成假的缺勤或遲到，HR 看不到原因；整檔失敗至少有失敗紀錄與監控警示，修好檔案或解析規則後重匯即可。
/// </summary>
public static class PunchFileParser
{
    /// <summary>打卡機的台灣本地時間，固定 +08:00（資料庫以 check 限制）。</summary>
    public static readonly TimeSpan MachineOffset = TimeSpan.FromHours(8);

    private const int FieldCount = 5;
    private const int MaxListedErrors = 3;
    private const int CardMaxLength = 32;
    private const int CodeMaxLength = 16;

    /// <summary>每日檔的檔名是 <c>yyyyMMdd.txt</c>；其他檔案（備份、說明、暫存）一律不匯入。</summary>
    public static bool TryParseFileName(string fileName, out DateOnly date)
    {
        date = default;
        return fileName.Length == 12 && fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
            && TryDate(fileName[..8], out date);
    }

    public static string FileNameFor(DateOnly date) => date.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".txt";

    /// <summary>
    /// 把檔案位元組解碼成文字：UTF-8（可有 BOM）或有 BOM 的 UTF-16。無效的位元組直接拒絕而不是換成替代字元，
    /// 否則亂碼會悄悄變成卡號或代碼存進去。
    /// </summary>
    public static string Decode(byte[] bytes)
    {
        try
        {
            using var reader = new StreamReader(new MemoryStream(bytes), new UTF8Encoding(false, throwOnInvalidBytes: true), detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }
        catch (DecoderFallbackException)
        {
            throw new PunchFileFormatException("檔案編碼無法辨識，需為 UTF-8 或 UTF-16 文字。");
        }
    }

    /// <summary>
    /// 解析整個檔案。空白行與只有 Ctrl-Z（舊系統的檔尾標記）的行略過；同一筆刷卡（卡號、時間、機器代號相同）在同一檔案重複出現只留第一筆。
    /// 時間依 <see cref="MachineOffset"/> 解讀，出勤日取刷卡時間的日期，與檔名無關。
    /// </summary>
    public static IReadOnlyList<ParsedPunch> Parse(string text)
    {
        var punches = new List<ParsedPunch>();
        var seen = new HashSet<(string Card, long Ticks, string Machine)>();
        var errors = new List<string>();
        var invalid = 0;
        using var reader = new StringReader(text);
        var number = 0;
        for (var line = reader.ReadLine(); line is not null; line = reader.ReadLine())
        {
            number++;
            if (string.IsNullOrWhiteSpace(line.Trim('\u001A'))) continue;
            var (punch, problem) = ParseLine(line);
            if (punch is null)
            {
                invalid++;
                if (errors.Count < MaxListedErrors) errors.Add($"第 {number} 行{problem}");
                continue;
            }
            if (seen.Add((punch.CardNumber.ToUpperInvariant(), punch.PunchedAt.UtcTicks, punch.MachineCode.ToUpperInvariant()))) punches.Add(punch);
        }
        if (invalid > 0)
            throw new PunchFileFormatException($"{invalid} 行格式不正確（{string.Join("；", errors)}{(invalid > errors.Count ? "…" : "")}），整個檔案未匯入。");
        return punches;
    }

    private static (ParsedPunch? Punch, string Problem) ParseLine(string line)
    {
        var fields = line.Split(':');
        if (fields.Length != FieldCount) return (null, $"欄位數是 {fields.Length}，應為 {FieldCount}");
        var machine = fields[0].Trim();
        var card = fields[1].Trim();
        var eventCode = fields[4].Trim();
        if (!IsToken(machine, CodeMaxLength)) return (null, "的機器代號是空的、過長或含控制字元");
        if (!IsToken(card, CardMaxLength)) return (null, "的卡號是空的、過長或含控制字元");
        if (!IsToken(eventCode, CodeMaxLength)) return (null, "的事件代號是空的、過長或含控制字元");
        if (!TryDate(fields[2].Trim(), out var date)) return (null, "的日期不是有效的 yyyyMMdd");
        if (!TryTime(fields[3].Trim(), out var time)) return (null, "的時間不是有效的 HHmmss");
        return (new(card, new DateTimeOffset(date.ToDateTime(time), MachineOffset), date, machine, eventCode), "");
    }

    private static bool IsToken(string value, int maxLength) => value.Length > 0 && value.Length <= maxLength && !value.Any(char.IsControl);

    // 年份限定在合理範圍：打卡機時鐘重設會輸出 2000 年之前的日期，這種資料進了資料庫只會讓出勤日錯亂。
    private static bool TryDate(string text, out DateOnly date)
    {
        date = default;
        return text.Length == 8 && text.All(char.IsAsciiDigit)
            && DateOnly.TryParseExact(text, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date) && date.Year is >= 2000 and <= 2099;
    }

    private static bool TryTime(string text, out TimeOnly time)
    {
        time = default;
        return text.Length == 6 && text.All(char.IsAsciiDigit) && TimeOnly.TryParseExact(text, "HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
    }
}
