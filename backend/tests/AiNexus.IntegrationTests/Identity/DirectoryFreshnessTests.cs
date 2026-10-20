using System.Net;
using System.Net.Http.Json;
using AiNexus.Data.App.Identity;
using AiNexus.Features.Admin.Identity.Authentication;
using static AiNexus.IntegrationTests.Support.IdentityApi;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;

namespace AiNexus.IntegrationTests.Identity;

public sealed class DirectoryFreshnessTests
{
    [Theory]
    [InlineData(1, true, false)]
    [InlineData(31, true, true)]
    [InlineData(31, false, false)]
    [InlineData(null, true, true)]
    public async Task LocalPasswordsOfDirectoryUsersRequireARecentDirectorySignIn(int? daysAgo, bool adEnabled, bool rejected)
    {
        using var passwords = new Argon2Passwords(AppFactory.PasswordCost);
        var hash = await passwords.HashAsync(Password, CancellationToken.None);
        await using var f = new AppFactory(ldap: true, seed: db => db.Users.Add(new User
        {
            Sid = "S-1-5-21-fixture-carol", Account = "carol@fixture.test", DisplayName = "carol", AdEnabled = adEnabled, LocalEnabled = true,
            LocalAccount = "CAROL", PasswordHash = hash, LastSeenAt = DateTimeOffset.UtcNow,
            DirectoryVerifiedAt = daysAgo is { } days ? DateTimeOffset.UtcNow.AddDays(-days) : null,
        }));
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
