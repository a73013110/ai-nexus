using AiNexus.Features.Admin.Identity.Authentication;
using AiNexus.Platform.Errors;

namespace AiNexus.IntegrationTests.Support;

/// <summary>測試用目錄：alice 有 AD 信箱、bob 沒有（可自行設定信箱）；密碼一律 <c>fixture-password</c>。</summary>
public sealed class FixtureAdAuthenticator : IAdAuthenticator
{
    public Task VerifyServiceAsync(CancellationToken ct) => Task.CompletedTask;
    public Task<Result<AdIdentity>> AuthenticateAsync(string account, string password, CancellationToken ct)
        => Task.FromResult(password == "fixture-password" && account is "alice" or "bob"
            ? Result<AdIdentity>.Ok(new AdIdentity($"S-1-5-21-fixture-{account}", $"{account}@fixture.test", account, account == "alice" ? "Alice@Fixture.TEST" : null))
            : Error.Unauthenticated("ad_invalid_credentials"));
}
