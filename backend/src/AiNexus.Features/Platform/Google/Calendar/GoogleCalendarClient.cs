using AiNexus.Platform.Modules;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Platform.Google.Calendar;

/// <summary>
/// Google Calendar API v3 的客戶端，以服務帳戶操作（<see cref="GoogleOptions"/>）。每個方法都回傳 <see cref="GoogleResponse{T}"/>、不為預期內的失敗拋例外：
/// 呼叫端依 <see cref="GoogleStatus"/> 決定接手（409）、重建（404／410）、退避（429、5xx）或停止（憑證、權限）。
/// 測試以假實作取代整個介面，不連 Google。
/// </summary>
public interface IGoogleCalendar
{
    /// <summary>
    /// 讀日曆的名稱與時區，用來確認日曆 ID 正確、服務帳戶有權限。走事件清單（取 0 筆）而不是 <c>calendars.get</c>：
    /// 後者需要整個日曆的範圍，服務帳戶只拿 <see cref="GoogleCalendarClient.Scope"/>。
    /// </summary>
    Task<GoogleResponse<GoogleCalendarInfo>> GetCalendarAsync(string calendarId, CancellationToken ct);

    /// <summary>建立事件；<see cref="GoogleCalendarEvent.Id"/> 已被使用（含已刪除的事件）時回 <see cref="GoogleStatus.Conflict"/>。</summary>
    Task<GoogleResponse<GoogleCalendarEvent>> InsertEventAsync(string calendarId, GoogleCalendarEvent value, CancellationToken ct);

    /// <summary>整筆取代事件內容；事件不存在回 <see cref="GoogleStatus.NotFound"/>。</summary>
    Task<GoogleResponse<GoogleCalendarEvent>> UpdateEventAsync(string calendarId, string eventId, GoogleCalendarEvent value, CancellationToken ct);

    /// <summary>刪除事件；已刪除或不存在時回 <see cref="GoogleStatus.Gone"/> 或 <see cref="GoogleStatus.NotFound"/>，呼叫端可當成完成。</summary>
    Task<GoogleResponse<bool>> DeleteEventAsync(string calendarId, string eventId, CancellationToken ct);

    /// <summary>以私有屬性找事件（不含已刪除），最多一頁 50 筆：用來找回呼叫端以前建立、卻沒記下 ID 的事件。</summary>
    Task<GoogleResponse<IReadOnlyList<GoogleCalendarEvent>>> FindEventsAsync(string calendarId, string property, string value, CancellationToken ct);
}

[Singleton<IGoogleCalendar>]
public sealed class GoogleCalendarClient(IHttpClientFactory clients, IOptions<GoogleOptions> options) : IGoogleCalendar
{
    public const string HttpClientName = "Google.Calendar";

    /// <summary>讀寫事件需要的範圍；服務帳戶只拿到這一個。</summary>
    public const string Scope = "https://www.googleapis.com/auth/calendar.events";

    private HttpClient Http => clients.CreateClient(HttpClientName);
    private TimeSpan Timeout => TimeSpan.FromSeconds(options.Value.TimeoutSeconds);

    public Task<GoogleResponse<GoogleCalendarInfo>> GetCalendarAsync(string calendarId, CancellationToken ct)
        => GoogleHttp.SendAsync(Http, new(HttpMethod.Get, $"calendar/v3/calendars/{Escape(calendarId)}/events?maxResults=1&fields=summary,timeZone"), GoogleCalendarJson.Default.GoogleCalendarInfo, new(), Timeout, ct);

    public Task<GoogleResponse<GoogleCalendarEvent>> InsertEventAsync(string calendarId, GoogleCalendarEvent value, CancellationToken ct)
        => GoogleHttp.SendAsync(Http, new(HttpMethod.Post, $"calendar/v3/calendars/{Escape(calendarId)}/events?sendUpdates=none") { Content = Body(value) },
            GoogleCalendarJson.Default.GoogleCalendarEvent, new(), Timeout, ct);

    public Task<GoogleResponse<GoogleCalendarEvent>> UpdateEventAsync(string calendarId, string eventId, GoogleCalendarEvent value, CancellationToken ct)
        => GoogleHttp.SendAsync(Http, new(HttpMethod.Put, $"calendar/v3/calendars/{Escape(calendarId)}/events/{Escape(eventId)}?sendUpdates=none") { Content = Body(value) },
            GoogleCalendarJson.Default.GoogleCalendarEvent, new(), Timeout, ct);

    public Task<GoogleResponse<bool>> DeleteEventAsync(string calendarId, string eventId, CancellationToken ct)
        => GoogleHttp.SendAsync(Http, new(HttpMethod.Delete, $"calendar/v3/calendars/{Escape(calendarId)}/events/{Escape(eventId)}?sendUpdates=none"), null, true, Timeout, ct);

    public async Task<GoogleResponse<IReadOnlyList<GoogleCalendarEvent>>> FindEventsAsync(string calendarId, string property, string value, CancellationToken ct)
    {
        var path = $"calendar/v3/calendars/{Escape(calendarId)}/events?maxResults=50&privateExtendedProperty={Uri.EscapeDataString($"{property}={value}")}";
        var response = await GoogleHttp.SendAsync(Http, new(HttpMethod.Get, path), GoogleCalendarJson.Default.GoogleCalendarEventList, new(), Timeout, ct);
        return response.IsOk ? GoogleResponse<IReadOnlyList<GoogleCalendarEvent>>.Ok(response.Value!.Items ?? []) : response.As<IReadOnlyList<GoogleCalendarEvent>>();
    }

    private static JsonContent Body(GoogleCalendarEvent value) => JsonContent.Create(value, GoogleCalendarJson.Default.GoogleCalendarEvent);

    private static string Escape(string segment) => Uri.EscapeDataString(segment);
}
