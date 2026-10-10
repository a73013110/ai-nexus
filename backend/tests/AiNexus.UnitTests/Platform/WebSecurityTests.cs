using System.Net;
using AiNexus.Platform.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace AiNexus.UnitTests.Platform;

public sealed class WebSecurityTests
{
    [Fact]
    public void SecureCookiesAreDefaultAndHttpExceptionCannotApplyToProductionOrRemoteClients()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Security:AllowInsecureLocalhost"] = "true", ["Security:DisableHttpsRedirection"] = "true" }).Build();
        var environment = new Microsoft.Extensions.Hosting.Internal.HostingEnvironment { EnvironmentName = Environments.Production };
        Assert.Equal(CookieSecurePolicy.Always, WebSecurity.CookiePolicy(environment, config));
        environment.EnvironmentName = Environments.Development;
        Assert.Equal(CookieSecurePolicy.SameAsRequest, WebSecurity.CookiePolicy(environment, config));
        Assert.Equal(CookieSecurePolicy.Always, WebSecurity.CookiePolicy(environment, new ConfigurationBuilder().Build()));
        var http = new DefaultHttpContext(); http.Request.Host = new HostString("localhost");
        Assert.True(WebSecurity.IsLoopback(http.Request, IPAddress.Loopback));
        Assert.False(WebSecurity.IsLoopback(http.Request, IPAddress.Parse("192.168.1.1")));
        http.Request.Host = new HostString("evil.invalid"); Assert.False(WebSecurity.IsLoopback(http.Request, IPAddress.Loopback));
    }
}
