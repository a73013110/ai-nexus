using AiNexus.Platform.Events;

namespace AiNexus.Features.Identity.Sessions;

/// <summary>The browser session <paramref name="SessionId"/> of <paramref name="UserId"/> signed out.</summary>
public sealed record UserSignedOut(Guid UserId, Guid SessionId) : IDomainEvent;
