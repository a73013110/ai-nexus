using AiNexus.Features.Admin.Identity.Authentication;

namespace AiNexus.UnitTests.Identity;

public sealed class Argon2PasswordsTests
{
    private const string Password = "fixture long password 42!";

    [Fact]
    public async Task Argon2UsesRandomSaltRejectsWrongPasswordAndBoundsUntrustedParameters()
    {
        using var hash = new Argon2Passwords(new Argon2Cost(1024, 1));
        var first = await hash.HashAsync(Password, default); var second = await hash.HashAsync(Password, default);
        Assert.NotEqual(first, second); Assert.True((await hash.VerifyAsync(Password, first, default)).Valid);
        Assert.False((await hash.VerifyAsync("wrong", first, default)).Valid);
        Assert.False((await hash.VerifyAsync(Password, first.Replace("m=1024", "m=999999999"), default)).Valid);
        // 正式成本參數會在計算前就拒絕低於 OWASP 下限的既有雜湊，所以即使密碼正確，這裡也必須驗證失敗。
        using var production = new Argon2Passwords(Argon2Cost.Recommended);
        Assert.False((await production.VerifyAsync(Password, first, default)).Valid);
        Assert.False(Argon2Passwords.IsAcceptable("short"));
    }
}
