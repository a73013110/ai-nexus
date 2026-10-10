using AiNexus.Features.Identity.Authentication;

namespace AiNexus.UnitTests.Identity;

public sealed class LdapAuthenticatorTests
{
    [Fact]
    public void LdapFiltersEscapeInjectionCharacters()
        => Assert.Equal(@"a\2a\29\28\5cb\00", LdapAuthenticator.EscapeFilter("a*)(\\b\0"));

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
}
