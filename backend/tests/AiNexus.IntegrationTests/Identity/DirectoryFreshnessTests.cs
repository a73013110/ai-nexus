using System.Net;
using System.Net.Http.Json;
using AiNexus.Data.App.Identity;
using AiNexus.Features.Admin.Identity.Authentication;
using AiNexus.Features.Admin.Identity.SelfService;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.IdentityApi;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.Identity;

public sealed class DirectoryFreshnessTests
{
    [Theory]
    [InlineData(30, 1, true, false)]
    [InlineData(30, 31, true, true)]
    [InlineData(30, 31, false, false)]
    [InlineData(30, null, true, true)]
    // 預設 0＝不限制：多久沒用 AD 登入、甚至從未用 AD 登入都能用本地密碼。
    [InlineData(0, 400, true, false)]
    [InlineData(0, null, true, false)]
    public async Task LocalPasswordsOfDirectoryUsersRequireARecentDirectorySignInWhenEnabled(int freshnessDays, int? daysAgo, bool adEnabled, bool rejected)
    {
        using var passwords = new Argon2Passwords(AppFactory.PasswordCost);
        var hash = await passwords.HashAsync(Password, CancellationToken.None);
        await using var f = new AppFactory(ldap: true, seed: db => db.Users.Add(new User
        {
            Sid = "S-1-5-21-fixture-carol", AdAccount = "carol", DisplayName = "carol", AdEnabled = adEnabled, LocalEnabled = true,
            LocalAccount = "CAROL", PasswordHash = hash, LastSeenAt = DateTimeOffset.UtcNow,
            DirectoryVerifiedAt = daysAgo is { } days ? DateTimeOffset.UtcNow.AddDays(-days) : null,
        }), services: s => s.PostConfigure<SelfServiceOptions>(o => o.DirectoryFreshnessDays = freshnessDays));
        using var client = f.CreateClient();
        await Csrf(client);
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new AdLoginRequest("carol", Password, "local"));
        if (rejected) await AssertCodeAsync(response, HttpStatusCode.Forbidden, "directory_verification_required");
        else Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // 密碼錯誤時仍是一般的帳密錯誤，不透露目錄狀態。
        await Csrf(client);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/login", new AdLoginRequest("carol", "wrong password 1234", "local"))).StatusCode);
    }
}
