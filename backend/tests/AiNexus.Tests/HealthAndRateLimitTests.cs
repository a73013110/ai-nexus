using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiNexus.Features.Attachments;
using AiNexus.Features.Chat;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting.Internal;
using Xunit;

namespace AiNexus.Tests;

public sealed class HealthAndRateLimitTests
{
    [Fact]
    public async Task ReadinessAnswersAnonymouslyWithoutNamingChecks()
    {
        await using var factory = new NexusFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("""{"status":"ready"}""", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ReadinessFailsWhenTheDatabaseCannotBeOpenedOrIsNotConfigured()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"nexus-missing-{Guid.NewGuid():N}", "nexus.db");
        await using var services = new ServiceCollection().AddDbContext<NexusDbContext>(x => x.UseSqlite($"Data Source={missing};Mode=ReadOnly")).BuildServiceProvider();
        var scopes = services.GetRequiredService<IServiceScopeFactory>();
        var configured = new StorageReadiness(new ConfigurationBuilder().Build(), new HostingEnvironment { EnvironmentName = "Testing" });
        Assert.Equal(HealthStatus.Unhealthy, (await new DatabaseHealthCheck(scopes, configured, TimeProvider.System).CheckHealthAsync(new())).Status);

        var unconfigured = new StorageReadiness(new ConfigurationBuilder().Build(), new HostingEnvironment { EnvironmentName = "Production" });
        var result = await new DatabaseHealthCheck(scopes, unconfigured, TimeProvider.System).CheckHealthAsync(new());
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("storage_not_configured", result.Description);
    }

    [Fact]
    public async Task SendingIsLimitedPerUserWithRetryAfter()
        => await AssertPerUserLimitAsync("/api/v1/runs", ChatModule.SendsPerMinute, client => client.PostAsJsonAsync("/api/v1/runs", new { }));

    [Fact]
    public async Task UploadingIsLimitedPerUserWithRetryAfter()
        => await AssertPerUserLimitAsync("/api/v1/attachments", AttachmentsModule.UploadsPerMinute, client => client.PostAsync("/api/v1/attachments", new StringContent("")));

    // The limiter runs before binding, so requests the endpoint then rejects still count.
    private static async Task AssertPerUserLimitAsync(string path, int permits, Func<HttpClient, Task<HttpResponseMessage>> send)
    {
        await using var factory = new NexusFactory();
        var alice = await factory.SignedInAsync();
        for (var i = 0; i < permits; i++)
        {
            using var allowed = await send(alice);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, allowed.StatusCode);
        }
        using var limited = await send(alice);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.RetryAfter?.Delta > TimeSpan.Zero, path + " must say when to retry.");
        Assert.Equal("rate_limited", (await limited.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        var bob = await factory.SignedInAsync("bob");
        using var other = await send(bob);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, other.StatusCode);
    }
}
