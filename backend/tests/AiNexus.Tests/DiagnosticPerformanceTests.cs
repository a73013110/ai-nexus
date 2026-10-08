using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using AiNexus.Platform.Diagnostics;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AiNexus.Tests;

[Collection("Diagnostic integration")]
public sealed class DiagnosticPerformanceTests
{
    [Fact, Trait("Category", "Performance")]
    public async Task MeasureRequestThroughputLatencyAndMemoryWithBoundedPersistence()
    {
        // TestServer + SQLite isolates app/logging cost, not IIS networking or production SQL performance.
        var baseline = await MeasureAsync(false); var logging = await MeasureAsync(true);
        var report = new {
            measuredAtUtc = DateTimeOffset.UtcNow, environment = new { os = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(), dotnet = Environment.Version.ToString(), processors = Environment.ProcessorCount, gcAvailableBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes, optimized = typeof(DiagnosticPerformanceTests).Assembly.GetCustomAttributes(typeof(DebuggableAttribute), false).OfType<DebuggableAttribute>().All(x => !x.IsJITOptimizerDisabled) },
            workload = new { requests = 4000, concurrency = 8, warmup = 200, endpoint = "GET /health/live", transport = "ASP.NET TestServer in process", queryStore = "SQLite per-test database", otlp = false, sampling = 1, flushIntervalMs = 10, batchSize = 200, normalQueue = 8192, importantQueue = 2048 },
            baselineProviderDisabled = baseline, diagnostics = logging,
            limitations = "Single process local development measurement. Baseline disables the provider on the same code path. Does not establish IIS/SQL Server network or production capacity."
        };
        var path = Path.GetFullPath("../../../../../../artifacts/diagnostics-performance.json", AppContext.BaseDirectory); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Assert.Equal(0, logging.Lost); Assert.True(logging.PersistedEvents > 0); Assert.True(logging.P95Ms < 500, "Local fixture should remain responsive; see measured report rather than a production performance claim.");
    }
    private sealed record Measurement(double RequestsPerSecond, double P50Ms, double P95Ms, double P99Ms, long AllocatedBytes, long ManagedBytesBefore, long ManagedBytesAfter, long WorkingSetBytes, long FileEvents, long PersistedEvents, long Lost);
    private static async Task<Measurement> MeasureAsync(bool enabled)
    {
        await using var factory = new NexusFactory(services: services => { if (!enabled) services.RemoveAll<ILoggerProvider>(); }); using var client = factory.CreateClient();
        for (var i = 0; i < 200; i++) { using var response = await client.GetAsync("/health/live"); response.EnsureSuccessStatusCode(); }
        GC.Collect(); GC.WaitForPendingFinalizers(); var before = GC.GetTotalMemory(true); var allocated = GC.GetTotalAllocatedBytes(true); var samples = new double[4000];
        var watch = Stopwatch.StartNew();
        await Parallel.ForEachAsync(Enumerable.Range(0, samples.Length), new ParallelOptions { MaxDegreeOfParallelism = 8 }, async (i, ct) => {
            var start = Stopwatch.GetTimestamp(); using var response = await client.GetAsync("/health/live", ct); response.EnsureSuccessStatusCode(); samples[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        });
        watch.Stop(); Array.Sort(samples); var elapsed = watch.Elapsed.TotalSeconds;
        var health = factory.Services.GetRequiredService<DiagnosticHealth>();
        if (enabled) for (var i = 0; i < 500 && (health.QueueDepth > 0 || health.PendingBytes > 0 || health.Accepted > health.Written); i++) await Task.Delay(10);
        using var scope = factory.Services.CreateScope(); var rows = await scope.ServiceProvider.GetRequiredService<NexusDbContext>().Set<DiagnosticEvent>().LongCountAsync();
        return new(samples.Length / elapsed, samples[2000], samples[3800], samples[3960], GC.GetTotalAllocatedBytes(true) - allocated, before, GC.GetTotalMemory(false), Process.GetCurrentProcess().WorkingSet64, health.Written, rows, health.Lost);
    }
}
