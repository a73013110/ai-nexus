using AiNexus.Features.Admin.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.UnitTests.Identity;

public sealed class NegotiateRegistrationTests
{
    private static async Task<bool> RegistersNegotiateAsync(string? mode)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateEmptyApplicationBuilder(new() { EnvironmentName = "Testing", ContentRootPath = Path.GetTempPath() });
        if (mode is not null) builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Identity:ActiveDirectory:Mode"] = mode });
        IdentityModule.AddServices(builder);
        await using var services = builder.Services.BuildServiceProvider();
        return await services.GetRequiredService<IAuthenticationSchemeProvider>().GetSchemeAsync(NegotiateDefaults.AuthenticationScheme) is not null;
    }

    // Negotiate 在 IIS 上要求站台啟用 Windows 驗證，啟用後鎖定的 401 會讓瀏覽器跳出帳密框；Ldap 站台必須能關掉它。
    [Theory]
    [InlineData(null, false)]
    [InlineData("Ldap", false)]
    [InlineData("Windows", true)]
    public async Task OnlyWindowsModeRegistersNegotiate(string? mode, bool expected) => Assert.Equal(expected, await RegistersNegotiateAsync(mode));
}
