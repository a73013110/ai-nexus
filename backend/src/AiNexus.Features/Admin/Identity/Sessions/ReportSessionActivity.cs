using AiNexus.Features.Admin.Identity.Authentication;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Modules;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Identity.Sessions;

/// <summary>
/// 前端回報使用者還在操作（節流過）。只有這個端點會延後閒置鎖定，背景輪詢不會；
/// 已鎖定時它和其他 API 一樣被拒絕，舊分頁的回報不能解開鎖定。
/// </summary>
internal sealed class ReportSessionActivity(SessionTicket ticket, IOptions<AdAuthenticationOptions> options, IAntiforgery csrf) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder auth) => auth
        .MapPost("/activity", (HttpContext http, ReportSessionActivity handler, int? idleSeconds) => handler.HandleAsync(http, idleSeconds ?? 0))
        .NotAudited("節流的存活回報，不是使用者的動作").WithName("ReportSessionActivity");

    public async Task<Ok<AuthSessionDto>> HandleAsync(HttpContext http, int idleSeconds)
    {
        await ticket.TouchAsync(http, idleSeconds);
        return TypedResults.Ok(AuthSession.Describe(http, options.Value, csrf));
    }
}
