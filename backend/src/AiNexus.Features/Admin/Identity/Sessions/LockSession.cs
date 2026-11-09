using AiNexus.Features.Admin.Identity.Authentication;
using AiNexus.Features.Admin.Identity.SelfService;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Identity.Sessions;

/// <summary>
/// 使用者手動鎖定工作區，判定仍在伺服器：cookie 記下鎖定後，其他分頁的請求一樣被拒絕。
/// 鎖定中也要放行：否則第二次按下會被前端的閘門暫存，解鎖後重送又把工作區鎖起來。
/// </summary>
internal sealed class LockSession(SessionTicket ticket, IOptions<AdAuthenticationOptions> options, IAntiforgery csrf) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder auth) => auth
        .MapPost("/lock", (HttpContext http, LockSession handler) => handler.HandleAsync(http))
        .AllowWhileLocked().NotAudited("只影響自己的畫面，解鎖才需要驗證，由登入稽核記錄").WithName("LockSession");

    public async Task<Result<AuthSessionDto>> HandleAsync(HttpContext http)
    {
        // 測試身分不能解鎖（輸入的是被測者的密碼），鎖了就只能登出。
        var allowed = Reauthentication.Allowed(http);
        if (!allowed.IsSuccess) return allowed.Error;
        await ticket.LockAsync(http);
        return AuthSession.Describe(http, options.Value, csrf);
    }
}
