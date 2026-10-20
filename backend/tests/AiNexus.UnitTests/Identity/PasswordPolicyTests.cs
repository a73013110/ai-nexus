using AiNexus.Features.Admin.Identity.SelfService;

namespace AiNexus.UnitTests.Identity;

public sealed class PasswordPolicyTests
{
    [Theory]
    [InlineData("Password1234")]
    [InlineData("aaaaaaaaaaaaaa")]
    [InlineData("abababababab")]
    [InlineData("123456789012")]
    [InlineData("qwertyuiopas")]
    [InlineData("my chen account pass")]
    public void RejectsGuessablePasswords(string password) => Assert.False(PasswordPolicy.IsStrong(password, "CHEN"));

    [Fact]
    public void AcceptsLongUnrelatedPassphrases() => Assert.True(PasswordPolicy.IsStrong("藍色貓咪 喜歡 下午三點的陽光", "CHEN", null));
}
