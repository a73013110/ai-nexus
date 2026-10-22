using AiNexus.Features.Admin.Identity.Provisioning;

namespace AiNexus.UnitTests.Identity;

public sealed class LdapDirectoryTests
{
    [Fact]
    public void SearchFilterOnlyMatchesPeopleAndEscapesTheKeyword()
    {
        var filter = LdapDirectory.SearchFilter(" lin*)(sn=x ");
        Assert.StartsWith("(&(objectCategory=person)(objectClass=user)(|", filter, StringComparison.Ordinal);
        Assert.Contains(@"(sAMAccountName=lin\2a\29\28sn=x*)", filter, StringComparison.Ordinal);
        Assert.Contains(@"(displayName=*lin\2a\29\28sn=x*)", filter, StringComparison.Ordinal);
        Assert.DoesNotContain("(sn=x", filter, StringComparison.Ordinal);
    }

    [Fact]
    public void SidFilterListsEverySid()
        => Assert.Equal("(&(objectCategory=person)(objectClass=user)(|(objectSid=S-1-5-21-1-2-3-1101)(objectSid=S-1-5-21-1-2-3-1102)))",
            LdapDirectory.SidFilter(["S-1-5-21-1-2-3-1101", "S-1-5-21-1-2-3-1102"]));

    [Theory]
    [InlineData("CN=林可蘿,OU=平台部,OU=研發處,DC=example,DC=test", "研發處 › 平台部")]
    [InlineData(@"CN=Lin\, Carol,OU=R\2BD,DC=example,DC=test", "R+D")]
    [InlineData(@"CN=carol,OU=\E7\A0\94\E7\99\BC,DC=example,DC=test", "研發")]
    [InlineData("CN=carol,CN=Users,DC=example,DC=test", "Users")]
    [InlineData("CN=carol,DC=example,DC=test", "")]
    public void PathListsOrganizationalUnitsFromTheTop(string distinguishedName, string expected)
        => Assert.Equal(expected, LdapDirectory.PathOf(distinguishedName));
}
