using AiNexus.Features.Identity.Authentication;

namespace AiNexus.UnitTests.Identity;

public sealed class LdapAuthenticatorTests
{
    [Fact]
    public void LdapFiltersEscapeInjectionCharacters()
        => Assert.Equal(@"a\2a\29\28\5cb\00", LdapAuthenticator.EscapeFilter("a*)(\\b\0"));
}
