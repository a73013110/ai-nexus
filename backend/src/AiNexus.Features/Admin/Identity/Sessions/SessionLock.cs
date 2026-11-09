using Microsoft.AspNetCore.Authentication.Cookies;

namespace AiNexus.Features.Admin.Identity.Sessions;

/// <summary>
/// 鎖定時伺服器的行為：cookie 留著（這次請求視同未登入），API 一律回 401 <c>session_locked</c>，資料不會再出去；
/// 只有標了 <see cref="AllowWhileLocked"/> 的端點看得到身分，用來查狀態、解鎖或改用其他帳號。
/// </summary>
internal static class SessionLock
{
    private const string Rejected = "Identity.SessionLocked";
    private sealed class Allowed;

    /// <summary>此端點在鎖定時仍保有登入身分。</summary>
    public static RouteHandlerBuilder AllowWhileLocked(this RouteHandlerBuilder builder) => builder.WithMetadata(new Allowed());

    /// <summary>這個請求是否因鎖定被視同未登入；challenge 依此回 <c>session_locked</c> 而不是要求重新登入。</summary>
    public static bool IsRejected(HttpContext http) => http.Items.ContainsKey(Rejected);

    public static void Enforce(CookieValidatePrincipalContext context, SessionTicket ticket)
    {
        var http = context.HttpContext;
        if (ticket.LockedBy(context.Properties) is null || http.GetEndpoint()?.Metadata.GetMetadata<Allowed>() is not null) return;
        http.Items[Rejected] = true;
        context.RejectPrincipal();
    }
}
