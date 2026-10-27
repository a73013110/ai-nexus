using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Admin.Monitoring.Traffic;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Diagnostics;

namespace AiNexus.Features.Admin.Monitoring.Presence;

/// <summary>分頁關閉時立即從即時在線清單移除，而不是等待逾時。</summary>
internal sealed class LeavePresence(RuntimeTraffic traffic)
{
    public static void Map(RouteGroupBuilder api) => api
        .MapDelete("/presence/{sessionId:guid}", (Guid sessionId, ICurrentUser user, LeavePresence handler) =>
        {
            handler.Handle(user.Id, sessionId);
            return TypedResults.NoContent();
        }).WithMetadata(new SuppressSuccessfulRequestLog()).NotAudited("線上狀態心跳").WithName("LeavePresence");

    public void Handle(Guid user, Guid sessionId) => traffic.Leave(user, sessionId);
}
