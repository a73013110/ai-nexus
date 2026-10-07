using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace AiNexus.BuildingBlocks.Diagnostics;

/// <summary>Bootstrap failures happen before hosted workers/DI are available. Only fixed metadata is emitted.</summary>
public static class DiagnosticStartup
{
    public static async Task RecordAsync(Exception exception, IConfiguration configuration, IHostEnvironment environment)
    {
        var issue = Issues.NewCode(); using var health = new DiagnosticHealth();
        DiagnosticOptions options;
        try { options = configuration.GetSection("Diagnostics").Get<DiagnosticOptions>() ?? new(); if (!options.Valid()) options = new(); }
        catch (Exception) { options = new(); }
        try { _ = DiagnosticJournal.Resolve(options.Directory, environment); } catch (Exception) { options.Directory = ""; }
        try
        {
            using var journal = new DiagnosticJournal(Options.Create(options), health, environment);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await journal.AppendAsync([new DiagnosticEvent {
                Level = LogLevel.Critical, Category = "AiNexus.Startup", EventId = DiagnosticEvents.Configuration.Id, EventName = DiagnosticEvents.Configuration.Name!,
                MessageTemplate = "Service failed to initialize; inspect masked diagnostic classification.", Service = options.ServiceName,
                Environment = environment.EnvironmentName, Version = DiagnosticLoggerProvider.Version, Instance = System.Environment.MachineName + "-" + System.Environment.ProcessId,
                TraceId = ActivityTraceId.CreateRandom().ToHexString(), SpanId = ActivitySpanId.CreateRandom().ToHexString(), IssueCode = issue, ErrorCode = "startup_failed",
                ExceptionType = DiagnosticRedactor.Text(exception.GetType().FullName, 180), ExceptionDetail = DiagnosticRedactor.Exception(exception)
            }], timeout.Token);
        }
        catch (Exception) { health.Emergency("startup_journal_failed", 1); }
        // The opaque code is also available to the service operator when the app cannot start.
        try { Console.Error.WriteLine("AiNexus startup failed. Issue: " + issue); } catch (IOException) { }
    }
}
