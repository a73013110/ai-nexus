using AiNexus.Features.Admin.Identity.Users;

namespace AiNexus.UnitTests.Identity;

public sealed class UserEmailsTests
{
    [Theory]
    [InlineData(" Chen@Company.COM ", "Chen@company.com")]
    [InlineData("a.b-c@mail.company.com.tw", "a.b-c@mail.company.com.tw")]
    public void AcceptsPlainAddresses(string input, string expected)
    {
        Assert.True(UserEmails.TryNormalize(input, out var address));
        Assert.Equal(expected, address);
    }

    [Theory]
    [InlineData("")]
    [InlineData("chen")]
    [InlineData("chen@localhost")]
    [InlineData("\"陳\" <chen@company.com>")]
    [InlineData("a@company.com, b@company.com")]
    [InlineData("chen@-company.com")]
    [InlineData("chen@company.com\r\nBcc: x@evil.test")]
    public void RejectsAnythingButASingleAddress(string input) => Assert.False(UserEmails.TryNormalize(input, out _));

    [Fact]
    public void MasksTheLocalPart()
    {
        Assert.Equal("c***n@company.com", UserEmails.Mask("chen@Company.com"));
        Assert.Equal("*@company.com", UserEmails.Mask("c@company.com"));
    }
}
