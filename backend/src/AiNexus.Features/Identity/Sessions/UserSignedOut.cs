using AiNexus.Platform.Events;

namespace AiNexus.Features.Identity.Sessions;

/// <summary>某使用者的一個瀏覽器 session（<paramref name="SessionId"/>）已登出。</summary>
public sealed record UserSignedOut(Guid UserId, Guid SessionId) : IDomainEvent;
