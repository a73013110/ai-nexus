namespace AiNexus.Features.Identity.Authentication;

public sealed record AdIdentity(string Sid, string Account, string DisplayName);

public interface IAdAuthenticator
{
    Task<AdIdentity> AuthenticateAsync(string account, string password, CancellationToken ct);
    Task VerifyServiceAsync(CancellationToken ct);
}
