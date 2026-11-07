using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using AiNexus.Data.App.CalendarSync;
using AiNexus.Features.Platform.Gd;
using AiNexus.Features.Platform.Google.Calendar;

namespace AiNexus.Features.Office.CalendarSync.Sync;

/// <summary>
/// 一段請假或出差期間在 Google 日曆上的樣子，以及它的雜湊。雜湊涵蓋送給 Google 的全部內容，
/// 所以改了這裡的格式（標題、說明、顏色）後，已同步的事件會在下一輪自動更新一次，不必另外寫轉換。
/// </summary>
/// <remarks>
/// 說明只寫代理人、狀態與文號，不寫假別與事由：日曆是全公司看得到的，病假、喪假等假別不該公開（舊系統也刻意拿掉）。
/// 不寫「最近同步時間」：每次同步都改內容會讓看日曆的人以為表單有異動，同步時間在管理頁看。
/// </remarks>
public static class CalendarEventContent
{
    public const string TimeZone = "Asia/Taipei";

    /// <summary>事件私有屬性：哪個系統建立的、對應哪一列。以後與 Google 對帳時，以來源找出本系統的事件。</summary>
    public const string SourceProperty = "source";
    public const string SourceValue = "ainexus";
    public const string RowProperty = "rowId";

    /// <summary>舊系統（ADAuthentication）寫在事件私有屬性的本地唯一碼。</summary>
    public const string LegacyUuidProperty = "UUID";

    public static GoogleCalendarEvent Render(Guid rowId, string? googleEventId, AbsenceSegment segment) => new()
    {
        Id = googleEventId,
        Status = GoogleCalendarEvent.Confirmed,
        Summary = segment.Kind == CalendarEventKinds.Travel ? $"【出差】{segment.ApplicantName}" : $"【請假】{segment.ApplicantName}",
        Description = Description(segment),
        Start = new() { DateTime = segment.StartsAt, TimeZone = TimeZone },
        End = new() { DateTime = segment.EndsAt, TimeZone = TimeZone },
        // 與舊系統相同：請假 1（薰衣草紫）、出差 4（紅鶴粉），看日曆的人已經習慣。
        ColorId = segment.Kind == CalendarEventKinds.Travel ? "4" : "1",
        ExtendedProperties = new() { Private = new() { [SourceProperty] = SourceValue, [RowProperty] = rowId.ToString("N") } },
    };

    /// <summary>送給 Google 的內容（不含事件 ID 與列 ID）的 SHA-256。欄位以不會出現在內容裡的分隔字元串起來，換行不同就是不同。</summary>
    public static byte[] Hash(AbsenceSegment segment)
    {
        var value = Render(Guid.Empty, null, segment);
        var text = string.Join('\u001f', value.Summary, value.Description, value.ColorId, value.Start!.DateTime!.Value.ToString("O"), value.End!.DateTime!.Value.ToString("O"), TimeZone);
        var bytes = ArrayPool<byte>.Shared.Rent(Encoding.UTF8.GetMaxByteCount(text.Length));
        try
        {
            return SHA256.HashData(bytes.AsSpan(0, Encoding.UTF8.GetBytes(text, bytes)));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bytes);
        }
    }

    private static string Description(AbsenceSegment segment)
    {
        var lines = new StringBuilder();
        if (segment.AgentName is { } agent) lines.Append("代理人：").Append(agent).Append('\n');
        lines.Append("狀態：").Append(segment.FlowStatus == nameof(GdFormFlowOver.Decide) ? "已核准" : "簽核中").Append('\n');
        lines.Append("文號：").Append(segment.DesNo);
        return lines.ToString();
    }
}
