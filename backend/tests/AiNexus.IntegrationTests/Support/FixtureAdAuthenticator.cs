using AiNexus.Features.Admin.Identity.Authentication;
using AiNexus.Platform.Errors;

namespace AiNexus.IntegrationTests.Support;

/// <summary>
/// 測試用目錄：alice 有 AD 信箱、bob 沒有（可自行設定信箱）；密碼一律 <c>fixture-password</c>。
/// 帳號比對不分大小寫、回傳目錄裡的小寫名稱，與 AD 的 sAMAccountName 查詢相同；步升驗證送來的是大寫的 <c>AdAccount</c>。
/// </summary>
public sealed class FixtureAdAuthenticator : IAdAuthenticator
{
    public Task VerifyServiceAsync(CancellationToken ct) => Task.CompletedTask;
    public Task<Result<AdIdentity>> AuthenticateAsync(string account, string password, CancellationToken ct)
    {
        var name = account.ToLowerInvariant();
        return Task.FromResult(password == "fixture-password" && name is "alice" or "bob"
            ? Result<AdIdentity>.Ok(new AdIdentity($"S-1-5-21-fixture-{name}", $"{name}@fixture.test", name, name == "alice" ? "Alice@Fixture.TEST" : null))
            : Error.Unauthenticated("ad_invalid_credentials"));
    }
}
