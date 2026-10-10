using AiNexus.Features.Identity.Authentication;

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
        // Production rejects stored hashes below the OWASP minimum before doing any work.
        using var production = new Argon2Passwords(Argon2Cost.Recommended);
        Assert.False((await production.VerifyAsync(Password, first, default)).Valid);
        Assert.False(Argon2Passwords.IsAcceptable("short"));
    }
}
