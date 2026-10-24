using System.Globalization;
using AiNexus.Features.Admin.Identity.Provisioning;

namespace AiNexus.UnitTests.Identity;

public sealed class LdapDirectoryTests
{
    [Fact]
    public void SidFilterListsEverySid()
        => Assert.Equal("(&(&(objectCategory=person)(objectClass=user))(|(objectSid=S-1-5-21-1-2-3-1101)(objectSid=S-1-5-21-1-2-3-1102)))",
            LdapDirectory.SidFilter(["S-1-5-21-1-2-3-1101", "S-1-5-21-1-2-3-1102"]));

    [Fact]
    public void AccountFilterMatchesTheExactEscapedAccountName()
        => Assert.Equal("(&(&(objectCategory=person)(objectClass=user))(sAMAccountName=carol\\2a))", LdapDirectory.AccountFilter(" carol* "));

    [Fact]
    public void PeopleFilterEscapesTheQueryAndSearchesTheListedFields()
    {
        Assert.Equal(LdapDirectory.PersonFilter, LdapDirectory.PeopleFilter("  "));
        var filter = LdapDirectory.PeopleFilter(" 林*)(x ");
        Assert.StartsWith("(&(&(objectCategory=person)(objectClass=user))(|(displayName=*林\\2a\\29\\28x*)", filter, StringComparison.Ordinal);
        Assert.Contains("(title=*林\\2a\\29\\28x*)", filter, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("OU=Staff, DC=example, DC=test", "OU=Staff,DC=example,DC=test")]
    [InlineData(" OU = Staff ,DC=example ", "OU=Staff,DC=example")]
    [InlineData(@"OU=R\, D,DC=example", @"OU=R\, D,DC=example")]
    public void CanonicalDropsSpacesAroundSeparatorsOnly(string configured, string expected)
        => Assert.Equal(expected, DirectoryNames.Canonical(configured));

    [Fact]
    public void NodeFilterHidesAdvancedViewContainers()
        => Assert.Contains("(!(showInAdvancedViewOnly=TRUE))", LdapDirectory.NodeFilter, StringComparison.Ordinal);

    [Theory]
    [InlineData("CN=林可蘿,OU=平台部,OU=研發處,DC=example,DC=test", "研發處 › 平台部")]
    [InlineData(@"CN=Lin\, Carol,OU=R\2BD,DC=example,DC=test", "R+D")]
    [InlineData(@"CN=carol,OU=\E7\A0\94\E7\99\BC,DC=example,DC=test", "研發")]
    [InlineData("CN=carol,CN=Users,DC=example,DC=test", "Users")]
    [InlineData("CN=carol,DC=example,DC=test", "")]
    public void PathListsOrganizationalUnitsFromTheTop(string distinguishedName, string expected)
        => Assert.Equal(expected, DirectoryNames.PathOf(distinguishedName));

    [Fact]
    public void NamesAndAncestryIgnoreCase()
    {
        Assert.Equal("Lin, Carol", DirectoryNames.NameOf(@"CN=Lin\, Carol,OU=R\2BD,DC=example,DC=test"));
        Assert.True(DirectoryNames.Within("OU=平台部,ou=研發處,DC=example,DC=test", "OU=研發處,DC=Example,DC=Test"));
        Assert.True(DirectoryNames.Within("DC=example,DC=test", "dc=example,dc=test"));
        Assert.False(DirectoryNames.Below("DC=example,DC=test", "DC=example,DC=test"));
        Assert.False(DirectoryNames.Within("OU=x,DC=notexample,DC=test", "DC=example,DC=test"));
    }

    [Theory]
    [InlineData("20261013013002.0Z", "2026-10-13T01:30:02+00:00")]
    [InlineData("bad", null)]
    public void WhenChangedReadsGeneralizedTime(string value, string? expected)
        => Assert.Equal(expected is null ? null : DateTimeOffset.Parse(expected, CultureInfo.InvariantCulture), LdapDirectory.GeneralizedTime(value));

    [Theory]
    [InlineData(0x200, null, 0x200)]
    [InlineData(0x200, 0x10, 0x210)]
    [InlineData(0x202, 0x80_0010, 0x80_0212)]
    [InlineData(0x200, 0x2, 0x200)]
    public void AccountControlAddsOnlyTheComputedLockoutAndExpiredBits(int stored, int? computed, int expected)
        => Assert.Equal(expected, LdapDirectory.AccountControl(stored, computed));

    [Fact]
    public void LastLogonReadsFileTimeAndTreatsZeroAsNever()
    {
        var time = new DateTimeOffset(2026, 10, 13, 1, 30, 2, TimeSpan.Zero);
        Assert.Equal(time, LdapDirectory.FileTime(time.ToFileTime().ToString(CultureInfo.InvariantCulture)));
        Assert.Null(LdapDirectory.FileTime("0"));
        Assert.Null(LdapDirectory.FileTime("9223372036854775807"));
    }
}
