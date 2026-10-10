using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Errors;
using AiNexus.UnitTests.Support;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiNexus.UnitTests.Platform;

public sealed class DiagnosticLoggerProviderTests
{
    private const string Secret = "private-provider-password-123";

    [Fact]
    public void CodeBearingRejectionsBypassMinimumAndSamplingAndRetainTrustedCorrelation()
    {
        using var health = new DiagnosticHealth(); var options = Options.Create(new DiagnosticOptions { MinimumLevel = LogLevel.Critical, LowLevelSampleEvery = 1000 });
        var buffer = new DiagnosticBuffer(options, health);
        using var provider = new DiagnosticLoggerProvider(buffer, options, new EnvironmentFixture());
        using var factory = LoggerFactory.Create(x => x.SetMinimumLevel(LogLevel.Trace).AddProvider(provider));
        using var trace = DiagnosticTrace.Start("validation.fixture");
        var issues = new Issues(factory.CreateLogger<Issues>(), factory);
        var problem = issues.Problem(new ExternalServiceException(Error.Invalid("invalid_request"), Secret));
        var row = Assert.Single(buffer.Drain()); Assert.Equal(problem.IssueCode, row.IssueCode); Assert.Equal(LogLevel.Information, row.Level);
        Assert.Equal(trace.TraceId.ToHexString(), row.TraceId); Assert.Equal(0, health.Sampled); Assert.DoesNotContain(Secret, problem.Title);
    }

    [Fact]
    public void QueueLimitsAreVisibleAndErrorCriticalAreNotSampled()
    {
        using var health = new DiagnosticHealth(); var options = Options.Create(new DiagnosticOptions { QueueCapacity = 1, ImportantQueueCapacity = 2, LowLevelSampleEvery = 10 });
        var buffer = new DiagnosticBuffer(options, health);
        for (var i = 0; i < 20; i++) buffer.Enqueue(new() { Level = LogLevel.Debug });
        buffer.Enqueue(new() { Level = LogLevel.Error }); buffer.Enqueue(new() { Level = LogLevel.Critical }); buffer.Enqueue(new() { Level = LogLevel.Error });
        var rows = buffer.Drain(); Assert.Contains(rows, x => x.Level == LogLevel.Critical); Assert.Contains(rows, x => x.Level == LogLevel.Error);
        Assert.Equal(18, health.Sampled); Assert.Equal(2, health.Lost); Assert.Equal(0, health.QueueDepth); Assert.Equal("degraded", health.Snapshot().Status);
    }
}
