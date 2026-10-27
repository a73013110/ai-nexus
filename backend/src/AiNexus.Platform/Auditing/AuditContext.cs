using System.Diagnostics;
using AiNexus.Platform.Security;

namespace AiNexus.Platform.Auditing;

/// <summary>
/// 一筆稽核的情境：有效身分、實際操作者、追蹤識別、授權這次請求的功能、來源 IP 與時間。
/// 請求外（背景工作）的有效身分由呼叫端指定，其他欄位為 null。
/// </summary>
public sealed record AuditContext(Guid? OwnerId, Guid? ActorId, string? TraceId, Guid? OperationId, string? FeatureId, string? ClientAddress, DateTimeOffset At);

/// <summary>稽核情境的唯一來源；<c>AuditTrail</c> 與 <c>AuditEventInterceptor</c> 都從這裡取值。</summary>
/// <remarks>
/// ActorId 只取自已驗證請求的 claim；有效身分取自請求中已解析的使用者（<see cref="IRequestUser"/>）。
/// FeatureId 是授權這次請求的功能（功能授權成功時寫進 <see cref="FeatureItem"/>），功能使用量依它統計。
/// </remarks>
public sealed class AuditContextSource(TimeProvider clock, IHttpContextAccessor? http = null)
{
    /// <summary><c>HttpContext.Items</c> 的鍵：功能授權成功時放入授權的功能 ID。</summary>
    public const string FeatureItem = "audit.feature";
    public const int ClientAddressMaxLength = 45;

    public AuditContext Capture(Guid? owner = null)
    {
        var request = http?.HttpContext;
        var activity = Activity.Current;
        var address = request?.Connection.RemoteIpAddress;
        // IPv4 對應的 IPv6（::ffff:10.0.0.1）還原成 IPv4，稽核頁與搜尋才看得到使用者熟悉的寫法。
        if (address?.IsIPv4MappedToIPv6 == true) address = address.MapToIPv4();
        return new(
            owner ?? request?.RequestServices.GetService<IRequestUser>()?.ResolvedId,
            Guid.TryParse(request?.User.FindFirst(RequestClaims.ActorId)?.Value, out var actor) ? actor : null,
            activity?.TraceId.ToHexString(),
            Guid.TryParse(activity?.GetTagItem("operation.id")?.ToString(), out var operation) ? operation : null,
            request?.Items[FeatureItem] as string,
            address?.ToString() is { } text ? text[..Math.Min(text.Length, ClientAddressMaxLength)] : null,
            clock.GetUtcNow());
    }

    /// <summary>
    /// 記下授權這次請求的功能。群組與端點各有一個功能 policy 時（例如頁面＋動作），保留最具體的（ID 段數多的）那個。
    /// </summary>
    public static void Authorize(HttpContext context, string feature)
    {
        if (context.Items[FeatureItem] is string current && current.Count(c => c == '.') >= feature.Count(c => c == '.')) return;
        context.Items[FeatureItem] = feature;
    }
}
