using AiNexus.Features.Admin.Mail;

namespace AiNexus.UnitTests.Mail;

public sealed class MailAddressesTests
{
    [Theory]
    [InlineData("  Chen@Company.COM ", "Chen@company.com")]
    [InlineData("first.last+tag@mail.company.internal", "first.last+tag@mail.company.internal")]
    public void PlainAddressesAreTrimmedAndTheirDomainLowercased(string input, string expected)
    {
        Assert.True(MailAddresses.TryNormalize(input, out var address));
        Assert.Equal(expected, address);
    }

    [Theory]
    [InlineData("")]
    [InlineData("chen")]
    [InlineData("chen@localhost")]
    [InlineData("\"Chen\" <chen@company.com>")]
    [InlineData("chen@company.com, eve@evil.test")]
    [InlineData("chen@company.com\r\nBcc: eve@evil.test")]
    [InlineData("chen@-company.com")]
    public void DisplayNamesListsHeaderInjectionAndBareHostsAreRejected(string input)
        => Assert.False(MailAddresses.TryNormalize(input, out _));

    [Fact]
    public void MaskKeepsOnlyTheEdgesOfTheLocalPartAndHashIgnoresCase()
    {
        Assert.Equal("c***n@company.com", MailAddresses.Mask("chen@Company.com"));
        Assert.Equal("*@company.com", MailAddresses.Mask("c@company.com"));
        Assert.Equal(MailAddresses.Hash("Chen@Company.com"), MailAddresses.Hash("chen@company.com"));
        Assert.Equal(32, MailAddresses.Hash("chen@company.com").Length);
    }

    [Fact]
    public void AnEmptyDomainListAllowsEveryRecipient()
    {
        Assert.True(MailAddresses.Allowed("chen@partner.test", []));
        Assert.True(MailAddresses.Allowed("chen@company.com", ["company.com"]));
        Assert.False(MailAddresses.Allowed("chen@partner.test", ["company.com"]));
        Assert.False(MailAddresses.Allowed("chen@sub.company.com", ["company.com"]));
    }
}
