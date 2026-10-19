using AiNexus.Data.App.Identity;
using System.Security.Claims;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Admin.Identity.Sessions;
using AiNexus.Features.Admin.Monitoring.Traffic;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Validation;
using FluentValidation;

namespace AiNexus.Features.Admin.Monitoring.Presence;

public sealed record PresenceRequest(Guid SessionId, string Feature, string State);

internal sealed class PresenceRequestValidator : RequestValidator<PresenceRequest>
{
    public override string ProblemCode => "invalid_request";

    public PresenceRequestValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.Feature).Must(MonitoringVocabulary.ValidFeature).WithErrorCode("unknown");
        RuleFor(x => x.State).Must(x => x is "active" or "idle" or "background").WithErrorCode("unknown");
    }
}
public sealed record PresenceReceipt(bool Enabled, int HeartbeatSeconds);

/// <summary>
/// 分頁心跳：記錄使用者、粗略的瀏覽器與裝置名稱、位址與固定的功能名稱。
/// 原始 User-Agent、URL 與標題絕不進入監控資料。此端點有速率限制，且不列入請求日誌。
/// </summary>
internal sealed class HeartbeatPresence(RuntimeTraffic traffic)
{
    public static void Map(RouteGroupBuilder api) => api
        .MapPost("/presence", (PresenceRequest request, HttpContext http, ICurrentUser user, HeartbeatPresence handler) => TypedResults.Ok(handler.Handle(http, user.User, request)))
        .RequireRateLimiting(MonitoringModule.PresenceRateLimit).WithMetadata(new SuppressSuccessfulRequestLog()).WithName("HeartbeatPresence");

    public PresenceReceipt Handle(HttpContext http, User user, PresenceRequest request)
    {
        var ua = http.Request.Headers.UserAgent.ToString();
        var browser = ua.Contains("Edg/", StringComparison.Ordinal) ? "Edge" : ua.Contains("Firefox/", StringComparison.Ordinal) ? "Firefox" : ua.Contains("Chrome/", StringComparison.Ordinal) ? "Chrome" : ua.Contains("Safari/", StringComparison.Ordinal) ? "Safari" : "其他瀏覽器";
        var device = ua.Contains("Android", StringComparison.Ordinal) ? "Android" : ua.Contains("iPhone", StringComparison.Ordinal) || ua.Contains("iPad", StringComparison.Ordinal) ? "iOS" : ua.Contains("Windows", StringComparison.Ordinal) ? "Windows" : ua.Contains("Mac", StringComparison.Ordinal) ? "macOS" : ua.Contains("Linux", StringComparison.Ordinal) ? "Linux" : "未知裝置";
        // 雙棧監聽時 IPv4 常以 ::ffff: 對映形式出現，先還原成 IPv4 才與一般顯示一致。
        var ip = http.Connection.RemoteIpAddress; if (ip?.IsIPv4MappedToIPv6 == true) ip = ip.MapToIPv4();
        traffic.Presence(user, request, ip?.ToString() ?? "未知", browser, device,
            http.User.FindFirstValue(SessionIdentity.Method) is "local" ? "local" : "ad", SessionIdentity.IsTest(http.User));
        // 心跳間隔固定 25 秒，遠小於最短的 60 秒會話逾時，單次遺失不會誤判為離線。
        return new PresenceReceipt(traffic.Enabled, 25);
    }
}
