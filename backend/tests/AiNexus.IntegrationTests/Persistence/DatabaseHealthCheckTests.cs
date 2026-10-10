using System.Net;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting.Internal;

namespace AiNexus.IntegrationTests.Persistence;

public sealed class DatabaseHealthCheckTests
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
}
