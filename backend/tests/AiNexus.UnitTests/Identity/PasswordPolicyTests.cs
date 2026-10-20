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

    // 來自內嵌清單（不在手列補充中），且不分大小寫比對。
    [Theory]
    [InlineData("123qweasdzxc")]
    [InlineData("LeaveMeAlone")]
    [InlineData("AMSTERDAM420")]
    [InlineData("Princess1234")]
    public void RejectsPasswordsOnTheCommonList(string password) => Assert.False(PasswordPolicy.IsStrong(password));

    [Fact]
    public void AcceptsLongUnrelatedPassphrases() => Assert.True(PasswordPolicy.IsStrong("藍色貓咪 喜歡 下午三點的陽光", "CHEN", null));

    [Fact]
    public void AcceptsUncommonAsciiPassphrases() => Assert.True(PasswordPolicy.IsStrong("violet-harbor-lantern-73", "CHEN"));
}
