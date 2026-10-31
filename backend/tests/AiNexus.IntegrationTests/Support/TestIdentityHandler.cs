using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiNexus.IntegrationTests.Support;

public sealed class TestSchemeProvider : AuthenticationSchemeProvider
{
    public TestSchemeProvider(IOptions<AuthenticationOptions> options) : base(options)
    {
        RemoveScheme("Negotiate");
        AddScheme(new AuthenticationScheme("Negotiate", "Fixture Windows", typeof(TestIdentityHandler)));
    }
}

public sealed class TestIdentityHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var user = Request.Headers["X-Test-User"].ToString();
        if (user.Length == 0) return Task.FromResult(AuthenticateResult.NoResult());
        List<Claim> claims = [new(ClaimTypes.Name, $"TEST\\{user}"), new(ClaimTypes.PrimarySid, $"S-1-5-21-test-{user}")];
        // 預設像 LDAP 登入一樣帶目錄姓名；X-Test-Profile: none 模擬真正的 Windows 整合驗證，姓名與信箱要另查目錄。
        if (Request.Headers["X-Test-Profile"] != "none") claims.Add(new("display_name", user));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")), "Test")));
    }
    protected override Task HandleChallengeAsync(AuthenticationProperties properties) { Response.StatusCode = (int)HttpStatusCode.Unauthorized; return Task.CompletedTask; }
}
