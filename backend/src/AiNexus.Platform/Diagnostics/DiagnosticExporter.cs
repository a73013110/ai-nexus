using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;

namespace AiNexus.Platform.Diagnostics;

/// <summary>OTLP receives only already sanitized events; never register a second raw application logging provider.</summary>
public sealed class DiagnosticExporter : IDisposable
{
    private readonly ILoggerFactory? factory;
    private readonly ILogger? logger;
    private readonly MeterListener? listener;
    private readonly DiagnosticHealth health;
    public DiagnosticExporter(IOptions<DiagnosticOptions> options, DiagnosticHealth health)
    {
        this.health = health;
        if (!options.Value.OtlpEnabled) return;
        listener = new MeterListener { InstrumentPublished = (instrument, owner) => {
            if (instrument.Meter.Name == "otel.sdk.experimental" && instrument.Name == "otel.sdk.processor.log.processed") owner.EnableMeasurementEvents(instrument);
        } };
        listener.SetMeasurementEventCallback<long>((_, count, tags, _) => {
            foreach (var tag in tags)
                if (tag.Key == "error.type" && tag.Value is string reason && reason is "queue_full" or "already_shutdown")
                {
                    Interlocked.Add(ref health.ExportFailures, count); health.Emergency("otlp_queue_full"); break;
                }
        });
        listener.Start();
        var processor = new BatchLogRecordExportProcessor(new ObservedLogExporter(options.Value, health), maxQueueSize: 2048, scheduledDelayMilliseconds: 1000, exporterTimeoutMilliseconds: 2000, maxExportBatchSize: 200);
        factory = LoggerFactory.Create(logging => logging.SetMinimumLevel(LogLevel.Trace).AddOpenTelemetry(o => {
            o.IncludeScopes = true;
            o.IncludeFormattedMessage = true;
            o.SetResourceBuilder(ResourceBuilder.CreateEmpty().AddService(DiagnosticRedactor.Text(options.Value.ServiceName, 80), serviceVersion: DiagnosticRedactor.Text(DiagnosticLoggerProvider.Version, 80)));
            o.AddProcessor(new CorrelationProcessor()); o.AddProcessor(processor);
        }));
        logger = factory.CreateLogger("AiNexus.Sanitized");
    }
    public void Export(IReadOnlyList<DiagnosticEvent> events)
    {
        if (logger is null) return;
        foreach (var item in events)
        {
            try
            {
                DiagnosticRedactor.Normalize(item);
                using var scope = logger.BeginScope(new Dictionary<string, object?> {
                    ["LogId"] = item.LogId.ToString(), ["IssueCode"] = item.IssueCode, ["OriginalTimestamp"] = item.At.UtcDateTime,
                    ["OriginalTraceId"] = item.TraceId, ["OriginalSpanId"] = item.SpanId, ["Category"] = item.Category,
                    ["Properties"] = item.PropertiesJson, ["ExceptionType"] = item.ExceptionType, ["ExceptionDetail"] = item.ExceptionDetail,
                    ["RequestId"] = item.RequestId, ["OperationId"] = item.OperationId?.ToString(), ["JobId"] = item.JobId?.ToString(), ["RunId"] = item.RunId?.ToString(), ["Attempt"] = item.Attempt,
                    ["Method"] = item.Method, ["Route"] = item.Route, ["StatusCode"] = item.StatusCode, ["DurationMs"] = item.DurationMs,
                    ["ExternalService"] = item.ExternalService, ["ErrorCode"] = item.ErrorCode, ["UserId"] = item.UserId?.ToString(), ["Instance"] = item.Instance, ["Environment"] = item.Environment
                });
                var rendered = DiagnosticMessage.Render(item, includeProperties: true);
                if (logger.IsEnabled(item.Level))
                    logger.Log(item.Level, new EventId(item.EventId, item.EventName),
                        new[] { new KeyValuePair<string, object?>("{OriginalFormat}", item.MessageTemplate),
                            new KeyValuePair<string, object?>("RenderedMessage", rendered) }, null, (_, _) => rendered);
            }
            catch (Exception) { Interlocked.Increment(ref health.ExportFailures); health.Emergency("otlp_export_failed"); }
        }
    }
    private sealed class CorrelationProcessor : BaseProcessor<LogRecord>
    {
        public override void OnEnd(LogRecord record)
        {
            record.TraceId = default; record.SpanId = default;
            record.ForEachScope((scope, item) => {
                foreach (var property in scope)
                    if (property.Key == "OriginalTraceId" && property.Value is string trace && trace.Length == 32) item.TraceId = ActivityTraceId.CreateFromString(trace);
                    else if (property.Key == "OriginalSpanId" && property.Value is string span && span.Length == 16) item.SpanId = ActivitySpanId.CreateFromString(span);
                    else if (property.Key == "OriginalTimestamp" && property.Value is DateTime at) item.Timestamp = at;
            }, record);
        }
    }
    public void Dispose() { factory?.Dispose(); listener?.Dispose(); }
    private sealed class ObservedLogExporter(DiagnosticOptions options, DiagnosticHealth health) : BaseExporter<LogRecord>
    {
        private readonly OtlpLogExporter inner = new(new OtlpExporterOptions { Endpoint = new Uri(options.OtlpEndpoint.TrimEnd('/') + "/v1/logs"), Protocol = OtlpExportProtocol.HttpProtobuf, TimeoutMilliseconds = 2000 });
        public override ExportResult Export(in Batch<LogRecord> batch)
        {
            var result = inner.Export(batch);
            if (result == ExportResult.Failure) { Interlocked.Increment(ref health.ExportFailures); health.Emergency("otlp_export_failed"); }
            else health.Recovered("otlp");
            return result;
        }
        protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
    }
}

public static class DiagnosticRegistration
{
    /// <summary>
    /// Our own meters plus the framework's built-in ones (request duration by route template, Kestrel connections,
    /// rate-limiter and authorization outcomes, outgoing HTTP by host, EF Core query and SaveChanges counts, GC and
    /// thread pool). Their tags never carry SQL text, full URLs, users or client addresses, unlike the automatic
    /// tracing instrumentations, which stay off.
    /// </summary>
    public static readonly string[] Meters =
    [
        "AiNexus.Diagnostics", "AiNexus.Runtime",
        "Microsoft.AspNetCore.Hosting", "Microsoft.AspNetCore.Server.Kestrel", "Microsoft.AspNetCore.Routing",
        "Microsoft.AspNetCore.Diagnostics", "Microsoft.AspNetCore.RateLimiting", "Microsoft.AspNetCore.Authentication",
        "Microsoft.AspNetCore.Authorization", "System.Net.Http", "Microsoft.EntityFrameworkCore", "System.Runtime",
    ];

    public static void AddNexusDiagnostics(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<DiagnosticOptions>().BindConfiguration("Diagnostics").Validate(x => x.Valid(), "Invalid diagnostics limits or OTLP endpoint.").ValidateOnStart();
        builder.Services.AddSingleton<DiagnosticHealth>(); builder.Services.AddSingleton<DiagnosticBuffer>();
        builder.Services.AddSingleton<DiagnosticLoggerProvider>();
        builder.Logging.ClearProviders(); builder.Services.AddSingleton<ILoggerProvider>(sp => sp.GetRequiredService<DiagnosticLoggerProvider>());
        // Category defaults must not silently override the centralized minimum/error policy.
        builder.Logging.AddFilter<DiagnosticLoggerProvider>((_, level) => level != LogLevel.None);
        builder.Services.AddSingleton<DiagnosticJournal>(); builder.Services.AddSingleton<IDiagnosticJournal>(sp => sp.GetRequiredService<DiagnosticJournal>()); 
        builder.Services.AddSingleton<DiagnosticExporter>(); builder.Services.AddSingleton<Issues>();
        // Registered first so this worker stops last, after business workers have emitted terminal events.
        builder.Services.AddHostedService<DiagnosticWorker>();
        DiagnosticOptions configuration;
        try { configuration = builder.Configuration.GetSection("Diagnostics").Get<DiagnosticOptions>() ?? new(); if (!configuration.Valid()) configuration = new(); }
        catch (Exception) { configuration = new(); } // Options validation reports malformed configuration at startup.
        ResourceBuilder Resource() => ResourceBuilder.CreateEmpty().AddService(DiagnosticRedactor.Text(configuration.ServiceName, 80), serviceVersion: DiagnosticRedactor.Text(DiagnosticLoggerProvider.Version, 80));
        var telemetry = builder.Services.AddOpenTelemetry();
        telemetry.WithTracing(t => {
            t.SetResourceBuilder(Resource()).AddSource(DiagnosticTrace.SourceName).SetSampler(new AlwaysOnSampler());
            if (configuration.OtlpEnabled) t.AddOtlpExporter(o => { o.Endpoint = new Uri(configuration.OtlpEndpoint.TrimEnd('/') + "/v1/traces"); o.Protocol = OtlpExportProtocol.HttpProtobuf; o.TimeoutMilliseconds = 2000; });
        });
        telemetry.WithMetrics(m => { m.SetResourceBuilder(Resource()).AddMeter(Meters); if (configuration.OtlpEnabled) m.AddOtlpExporter(o => { o.Endpoint = new Uri(configuration.OtlpEndpoint.TrimEnd('/') + "/v1/metrics"); o.Protocol = OtlpExportProtocol.HttpProtobuf; o.TimeoutMilliseconds = 2000; }); });
    }
}
