using AiNexus.Features.Identity.Authentication;
using AiNexus.Platform.Errors;

namespace AiNexus.IntegrationTests.Support;

public sealed class FixtureAdAuthenticator : IAdAuthenticator
{
    public Task VerifyServiceAsync(CancellationToken ct) => Task.CompletedTask;
    public Task<Result<AdIdentity>> AuthenticateAsync(string account, string password, CancellationToken ct)
        => Task.FromResult(password == "fixture-password" && account is "alice" or "bob"
            ? Result<AdIdentity>.Ok(new AdIdentity($"S-1-5-21-fixture-{account}", $"{account}@fixture.test", account))
            : Error.Unauthenticated("ad_invalid_credentials"));
}
