using System.Text.Json.Serialization;

namespace AiNexus.Features.Platform.Google.Calendar;

/// <summary>
/// Google Calendar API v3 的事件（<see href="https://developers.google.com/calendar/api/v3/reference/events">Events resource</see>），只列用到的欄位。
/// 寫入時沒有值的欄位不送出；更新用整筆取代（PUT），沒送的欄位會被清空，所以呼叫端每次都送完整內容。
/// </summary>
public sealed record GoogleCalendarEvent
{
    /// <summary>
    /// 事件 ID：建立時可由呼叫端指定（小寫 a–v 與 0–9，5～1024 字元），同一個日曆不可重複，刪除後仍保留；
    /// 呼叫端用自己的主鍵當 ID，建立重試時只會得到 409，不會產生重複事件。
    /// </summary>
    public string? Id { get; init; }

    /// <summary><c>confirmed</c>、<c>tentative</c> 或 <c>cancelled</c>（已刪除）。更新時送 <c>confirmed</c> 可以救回已刪除的事件。</summary>
    public string? Status { get; init; }

    public string? Summary { get; init; }
    public string? Description { get; init; }
    public GoogleEventTime? Start { get; init; }
    public GoogleEventTime? End { get; init; }

    /// <summary>事件顏色（Calendar API 的 colors 編號 1～11）。</summary>
    public string? ColorId { get; init; }

    public GoogleExtendedProperties? ExtendedProperties { get; init; }

    /// <summary>Google 回傳的事件網址，唯讀。</summary>
    public string? HtmlLink { get; init; }

    /// <summary>Google 最後修改的時間，唯讀。</summary>
    public DateTimeOffset? Updated { get; init; }

    public const string Confirmed = "confirmed";
    public const string Cancelled = "cancelled";
}

/// <summary>事件的開始或結束時間；有時刻的事件用 <see cref="DateTime"/>（含時差），整天的事件用 <see cref="Date"/>（yyyy-MM-dd）。</summary>
public sealed record GoogleEventTime
{
    public DateTimeOffset? DateTime { get; init; }
    public string? Date { get; init; }

    /// <summary>IANA 時區（例如 <c>Asia/Taipei</c>），決定事件在日曆上如何顯示與重複；有時差的 <see cref="DateTime"/> 本身已足以表示時刻。</summary>
    public string? TimeZone { get; init; }
}

/// <summary>
/// 事件的自訂屬性。<see cref="Private"/> 只有這份日曆副本看得到，用來記下事件來自哪個系統、哪一筆資料，
/// 可用 <c>privateExtendedProperty</c> 查詢（<see cref="IGoogleCalendar.FindEventsAsync"/>）。
/// </summary>
public sealed record GoogleExtendedProperties
{
    public Dictionary<string, string>? Private { get; init; }
}

/// <summary>日曆的名稱與時區（事件清單回應的頂層欄位），連線測試用。</summary>
public sealed record GoogleCalendarInfo
{
    public string? Summary { get; init; }
    public string? TimeZone { get; init; }
}

internal sealed record GoogleCalendarEventList
{
    public List<GoogleCalendarEvent>? Items { get; init; }
    public string? NextPageToken { get; init; }
}

/// <summary>Calendar API 的 JSON 以 source generation 產生序列化程式碼（ADR 0030 不在執行期反射）：camelCase、沒有值的欄位不送出。</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(GoogleCalendarEvent))]
[JsonSerializable(typeof(GoogleCalendarEventList))]
[JsonSerializable(typeof(GoogleCalendarInfo))]
internal sealed partial class GoogleCalendarJson : JsonSerializerContext;
