using System.Net;
using AiNexus.Features.Monitoring;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace AiNexus.IntegrationTests.Monitoring;

public sealed class RuntimeSamplerTests
{
    [Fact]
    public async Task FactoryClientsShareDependencyObservationAndSuppressObserverCalls()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.Zero));
        await using var factory = new NexusFactory(services: services => {
            services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(clock);
            services.AddHttpClient("monitoring-probe").ConfigurePrimaryHttpMessageHandler(() => new ProbeHandler());
        });
        using var host = factory.CreateClient();
        using var client = factory.Services.GetRequiredService<IHttpClientFactory>().CreateClient("monitoring-probe");
        using var failed = await client.GetAsync("https://generativelanguage.googleapis.com/private?key=private");
        Assert.Equal(HttpStatusCode.BadGateway, failed.StatusCode);
        using (var suppression = new MonitoringSuppression()) {
            using var ignored = await client.GetAsync("https://generativelanguage.googleapis.com/observer");
        }
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetAsync("https://generativelanguage.googleapis.com/cancelled", cancelled.Token));
        clock.Advance(TimeSpan.FromSeconds(10));
        var snapshot = factory.Services.GetRequiredService<RuntimeTraffic>().Snapshot(1);
        var dependency = Assert.Single(snapshot.Dependencies, d => d.Id == "google");
        Assert.Equal(2, dependency.Metrics.Requests); Assert.Equal(1, dependency.Metrics.Errors);
        Assert.Equal(1, dependency.Metrics.Cancelled); Assert.Equal(0, dependency.InFlight);
        Assert.DoesNotContain("private", System.Text.Json.JsonSerializer.Serialize(snapshot));
    }

    private sealed class ProbeHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway));
        }
    }
}
