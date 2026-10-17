using AiNexus.Features.Identity;
using AiNexus.Features.Monitoring.Traffic;
using AiNexus.Platform.Diagnostics;

namespace AiNexus.Features.Monitoring.Presence;

/// <summary>分頁關閉時立即從即時在線清單移除，而不是等待逾時。</summary>
internal sealed class LeavePresence(RuntimeTraffic traffic)
{
    public static void Map(RouteGroupBuilder api) => api
        .MapDelete("/presence/{sessionId:guid}", (Guid sessionId, ICurrentUser user, LeavePresence handler) =>
        {
            handler.Handle(user.Id, sessionId);
            return TypedResults.NoContent();
        }).WithMetadata(new SuppressSuccessfulRequestLog()).WithName("LeavePresence");

    public void Handle(Guid user, Guid sessionId) => traffic.Leave(user, sessionId);
}
