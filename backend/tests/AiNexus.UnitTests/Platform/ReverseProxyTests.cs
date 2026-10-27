using System.Net;
using AiNexus.Platform.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AiNexus.UnitTests.Platform;

public sealed class ReverseProxyTests
{
    [Fact]
    public async Task TrustedProxyForwardsClientAddressAndScheme()
    {
        var http = await SendAsync(["192.0.2.0/24"], "::ffff:192.0.2.10", "198.51.100.7", "https");
        Assert.Equal(IPAddress.Parse("198.51.100.7"), http.ClientAddress());
        Assert.True(http.Request.IsHttps);
    }

    [Fact]
    public async Task DirectClientCannotSpoofForwardedHeaders()
    {
        var http = await SendAsync(["192.0.2.10"], "203.0.113.8", "198.51.100.7", "https");
        Assert.Equal(IPAddress.Parse("203.0.113.8"), http.ClientAddress());
        Assert.False(http.Request.IsHttps);
    }

    [Fact]
    public async Task ChainStopsAtFirstUntrustedHop()
    {
        // 客戶端自填 10.9.9.9，經代理附上真實來源 198.51.100.7；只能採信代理寫的那一段。
        var http = await SendAsync(["192.0.2.10"], "192.0.2.10", "10.9.9.9, 198.51.100.7", null);
        Assert.Equal(IPAddress.Parse("198.51.100.7"), http.ClientAddress());
    }

    [Fact]
    public async Task UnconfiguredSiteKeepsConnectionAddress()
    {
        var http = await SendAsync([], "::ffff:203.0.113.8", "198.51.100.7", null);
        Assert.Equal(IPAddress.Parse("203.0.113.8"), http.ClientAddress());
    }

    [Theory]
    [InlineData("proxy.company.internal")]
    [InlineData("10.0.0.0/33")]
    [InlineData("0.0.0.0/0")]
    [InlineData("::/0")]
    public void InvalidTrustedProxyFailsStartup(string item)
    {
        using var services = Services([item]);
        Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IOptions<ReverseProxyOptions>>().Value);
    }

    private static async Task<HttpContext> SendAsync(string[] trusted, string remote, string forwardedFor, string? forwardedProto)
    {
        using var services = Services(trusted);
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = IPAddress.Parse(remote);
        http.Request.Scheme = "http";
        http.Request.Headers["X-Forwarded-For"] = forwardedFor;
        if (forwardedProto is not null) http.Request.Headers["X-Forwarded-Proto"] = forwardedProto;
        var middleware = new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance,
            services.GetRequiredService<IOptions<ForwardedHeadersOptions>>());
        await middleware.Invoke(http);
        return http;
    }

    private static ServiceProvider Services(string[] trusted)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            trusted.Select((item, index) => KeyValuePair.Create<string, string?>($"{ReverseProxyOptions.Section}:TrustedProxies:{index}", item))).Build();
        return new ServiceCollection().AddSingleton<IConfiguration>(configuration).AddReverseProxy().BuildServiceProvider();
    }
}
