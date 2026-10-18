using AiNexus.Data.App;
using System.Net;
using AiNexus.Platform.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;

namespace AiNexus.IntegrationTests.Data;

public sealed class DatabaseHealthCheckTests
{
    [Fact]
    public async Task ReadinessAnswersAnonymouslyWithoutNamingChecks()
    {
        await using var factory = new AppFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("""{"status":"ready"}""", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ReadinessFailsWhenTheDatabaseCannotBeOpenedOrIsNotConfigured()
    {
        // 不存在的資料庫：伺服器拒絕登入，CanConnect 回傳 false。
        var missing = new SqlConnectionStringBuilder { DataSource = TestDatabase.Server, InitialCatalog = $"AiNexus_Missing_{Guid.NewGuid():N}", IntegratedSecurity = true, TrustServerCertificate = true, ConnectTimeout = 5 };
        await using var services = new ServiceCollection().AddDbContext<AppDbContext>(x => x.UseSqlServer(missing.ConnectionString)).BuildServiceProvider();
        var scopes = services.GetRequiredService<IServiceScopeFactory>();
        var configured = new StorageReadiness(Options.Create(new DatabasesOptions()), new HostingEnvironment { EnvironmentName = "Testing" });
        Assert.Equal(HealthStatus.Unhealthy, (await new DatabaseHealthCheck(scopes, configured, TimeProvider.System).CheckHealthAsync(new())).Status);

        var unconfigured = new StorageReadiness(Options.Create(new DatabasesOptions()), new HostingEnvironment { EnvironmentName = "Production" });
        var result = await new DatabaseHealthCheck(scopes, unconfigured, TimeProvider.System).CheckHealthAsync(new());
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("storage_not_configured", result.Description);
    }
}
