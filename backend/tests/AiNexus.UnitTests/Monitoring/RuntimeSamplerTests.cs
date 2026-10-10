using AiNexus.Features.Monitoring;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace AiNexus.UnitTests.Monitoring;

public sealed class RuntimeSamplerTests
{
    private static RuntimeTraffic Traffic(FakeTimeProvider clock, int capacity = 2000) => new(Options.Create(new MonitoringOptions { MaxSessions = capacity }), clock);

    [Fact]
    public void SqlObserverReadsOnlyConnectionClassificationAndTracksFailures()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.Zero)); var traffic = Traffic(clock);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Nexus"] = "Server=fixture;Database=fixture-db;Integrated Security=True" }).Build();
        var catalog = new DependencyCatalog(config, traffic);
        var observer = new RuntimeSampler(traffic, catalog, clock);
        using var connection = new SqlConnection(config.GetConnectionString("Nexus"));
        using var command = new SqlCommand("SELECT 'private-password-prompt'", connection);
        var operation = Guid.NewGuid();
        observer.OnNext(new KeyValuePair<string, object?>("Microsoft.Data.SqlClient.WriteCommandBefore", new { OperationId = operation, Command = command }));
        Assert.Equal(1, traffic.Snapshot(1).Dependencies.Single(x => x.Id == "sql.nexus").InFlight);
        observer.OnNext(new KeyValuePair<string, object?>("Microsoft.Data.SqlClient.WriteCommandError", new { OperationId = operation, Exception = new Exception("private") }));
        clock.Advance(TimeSpan.FromSeconds(10)); var dep = traffic.Snapshot(1).Dependencies.Single(x => x.Id == "sql.nexus");
        Assert.Equal("failing", dep.Status); Assert.Equal(1, dep.Metrics.Errors); Assert.Equal(0, dep.InFlight);
        Assert.DoesNotContain("private", System.Text.Json.JsonSerializer.Serialize(traffic.Snapshot(1)));
    }
}
