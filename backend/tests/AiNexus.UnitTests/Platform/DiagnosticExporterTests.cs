using System.Net;
using AiNexus.Platform.Diagnostics;
using AiNexus.UnitTests.Support;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiNexus.UnitTests.Platform;

public sealed class DiagnosticExporterTests
{
    [Fact]
    public async Task UnavailableOptionalExporterDoesNotLoseDurableEvents()
    {
        using var health = new DiagnosticHealth();
        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        var options = Options.Create(new DiagnosticOptions { OtlpEnabled = true, OtlpEndpoint = "http://127.0.0.1:" + port });
        using var exporter = new DiagnosticExporter(options, health);
        var directory = Path.Combine(Path.GetTempPath(), "nexus-otlp-" + Guid.NewGuid().ToString("N"));
        try {
            var fileOptions = Options.Create(new DiagnosticOptions { Directory = directory }); using var journal = new DiagnosticJournal(fileOptions, health, new EnvironmentFixture());
            using var trace = DiagnosticTrace.Start("export.fixture"); var row = new DiagnosticEvent { IssueCode = Issues.NewCode(), TraceId = trace.TraceId.ToHexString(), SpanId = trace.SpanId.ToHexString(), Level = LogLevel.Error, MessageTemplate = "Safe exporter fixture." };
            await journal.AppendAsync([row], CancellationToken.None); exporter.Export([row]);
            for (var attempt = 0; attempt < 100 && health.ExportFailures == 0; attempt++) await Task.Delay(50);
            Assert.True(health.ExportFailures > 0); var store = new MemoryStore(); await journal.ReplayAsync(store, CancellationToken.None);
            Assert.Single(store.Events); Assert.Equal(0, health.Lost); Assert.Equal(row.TraceId, store.Events[row.LogId].TraceId);
        } finally { Directory.Delete(directory, true); }
    }
}
