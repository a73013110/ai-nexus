using AiNexus.Features.Admin.Identity.Authentication;
using AiNexus.Features.Admin.Identity.Users;

namespace AiNexus.UnitTests.Identity;

public sealed class LdapAuthenticatorTests
{
    [Fact]
    public void LdapFiltersEscapeInjectionCharacters()
        => Assert.Equal(@"a\2a\29\28\5cb\00", LdapConnections.EscapeFilter("a*)(\\b\0"));

    [Fact]
    public void DomainDerivesServerSearchBaseAndBindName()
    {
        var options = new AdAuthenticationOptions { Domain = "company.internal", BindUser = "reader" };
        Assert.Equal("company.internal", options.Host);
        Assert.Equal(389, options.Port);
        Assert.Equal("DC=company,DC=internal", options.Base);
        Assert.Equal("reader@company.internal", options.BindName);
    }

    [Theory]
    [InlineData("reader@other.internal")]
    [InlineData(@"COMPANY\reader")]
    [InlineData("CN=reader,CN=Users,DC=company,DC=internal")]
    public void QualifiedBindUsersAreUsedAsGiven(string user)
        => Assert.Equal(user, new AdAuthenticationOptions { Domain = "company.internal", BindUser = user }.BindName);

    [Fact]
    public void ExplicitSettingsOverrideDerivedValues()
    {
        var options = new AdAuthenticationOptions { Domain = "company.internal", Server = "dc01.company.internal", UseLdaps = true, SearchBase = "OU=Staff,DC=company,DC=internal" };
        Assert.Equal("dc01.company.internal", options.Host);
        Assert.Equal(636, options.Port);
        Assert.Equal("OU=Staff,DC=company,DC=internal", options.Base);
    }

    [Theory]
    [InlineData("chen", "chen")]
    [InlineData(@"COMPANY\chen", "chen")]
    [InlineData("chen@company.internal", "chen")]
    [InlineData("chen@Company.COM", "chen")]
    [InlineData("chen@other.com", "chen@other.com")]
    [InlineData(@"A\B\chen", @"A\B\chen")]
    public void DirectoryLoginNamesDropOnlyKnownDomainParts(string input, string expected)
        => Assert.Equal(expected, UserAccounts.DirectoryLoginName(input, "company.internal", ["company.com"]));
}
