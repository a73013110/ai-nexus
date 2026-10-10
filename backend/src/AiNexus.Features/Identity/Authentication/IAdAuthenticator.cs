using AiNexus.Platform.Errors;

namespace AiNexus.Features.Identity.Authentication;

public sealed record AdIdentity(string Sid, string Account, string DisplayName);

/// <summary>The directory. Wrong credentials are an <see cref="Error"/>; an unreachable or misconfigured directory throws <see cref="ExternalServiceException"/>.</summary>
public interface IAdAuthenticator
{
    Task<Result<AdIdentity>> AuthenticateAsync(string account, string password, CancellationToken ct);
    Task VerifyServiceAsync(CancellationToken ct);
}
