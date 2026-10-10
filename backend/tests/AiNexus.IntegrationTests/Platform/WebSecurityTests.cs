using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Identity.Authentication;
using AiNexus.Features.Identity.Sessions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AiNexus.IntegrationTests.Platform;

public sealed class WebSecurityTests
{
    [Fact]
    public async Task SensitiveResponsesHaveConsistentHeadersAndTokenGenerationNeverLogsCacheOverrides()
    {
        var logs = new Logs();
        await using var factory = new NexusFactory(ldap: true, services: services => services.AddSingleton<ILoggerProvider>(logs));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var bootstrap = await client.GetAsync("/api/v1/auth/session");
        AssertNoStore(bootstrap);
        Assert.Contains(bootstrap.Headers.GetValues("Set-Cookie"), value => value.Contains("secure", StringComparison.OrdinalIgnoreCase) && value.Contains("httponly", StringComparison.OrdinalIgnoreCase) && value.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase));
        client.DefaultRequestHeaders.Add("X-Nexus-CSRF", (await bootstrap.Content.ReadFromJsonAsync<AuthSessionDto>())!.CsrfToken);
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new AdLoginRequest("alice", "fixture-password"));
        login.EnsureSuccessStatusCode(); AssertNoStore(login);
        Assert.Contains(login.Headers.GetValues("Set-Cookie"), value => value.StartsWith("Nexus.Session=", StringComparison.Ordinal) && value.Contains("secure", StringComparison.OrdinalIgnoreCase));
        using var me = await client.GetAsync("/api/v1/me"); me.EnsureSuccessStatusCode(); AssertNoStore(me);
        Assert.Equal("DENY", me.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("nosniff", me.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("same-origin", me.Headers.GetValues("Cross-Origin-Resource-Policy").Single());
        Assert.Contains("frame-ancestors 'none'", me.Headers.GetValues("Content-Security-Policy").Single());
        var csp = me.Headers.GetValues("Content-Security-Policy").Single();
        Assert.Contains("img-src 'self' blob:;", csp);
        Assert.Contains("script-src 'self';", csp);
        Assert.DoesNotContain("img-src *", csp);
        using var missing = await client.GetAsync("/api/v1/not-a-real-api"); Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode); AssertNoStore(missing);
        Assert.DoesNotContain(logs.Events, entry => entry.Category == "Microsoft.AspNetCore.Antiforgery.DefaultAntiforgery" && entry.Id == 8);
    }

    private static void AssertNoStore(HttpResponseMessage response)
    {
        Assert.True(response.Headers.CacheControl?.NoStore); Assert.True(response.Headers.CacheControl?.NoCache);
        Assert.Equal("no-cache", response.Headers.Pragma.Single().Name);
    }

    private sealed class Logs : ILoggerProvider
    {
        public ConcurrentQueue<(string Category, int Id)> Events { get; } = new();
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(categoryName, Events);
        public void Dispose() { }
        private sealed class CaptureLogger(string category, ConcurrentQueue<(string Category, int Id)> events) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> formatter) => events.Enqueue((category, id.Id));
        }
    }
}
