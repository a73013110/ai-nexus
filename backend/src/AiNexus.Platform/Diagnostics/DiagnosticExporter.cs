using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Options;
using AiNexus.Platform.Configuration;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;

namespace AiNexus.Platform.Diagnostics;

/// <summary>
/// OTLP 遙測日誌匯出器。只接收已清洗、已脫敏的日誌事件；絕不可再註冊第二個未脫敏的應用程式日誌提供者，否則敏感資料會外洩。
/// </summary>
public sealed class DiagnosticExporter : IDisposable
{
    private readonly ILoggerFactory? factory;
    private readonly ILogger? logger;
    private readonly MeterListener? listener;
    private readonly DiagnosticHealth health;

    public DiagnosticExporter(IOptions<DiagnosticOptions> options, DiagnosticHealth health)
    {
        this.health = health;
        if (!options.Value.OtlpEnabled)
        {
            return;
        }

        listener = new MeterListener
        {
            InstrumentPublished = (instrument, owner) =>
            {
                if (instrument.Meter.Name == "otel.sdk.experimental" && instrument.Name == "otel.sdk.processor.log.processed")
                {
                    owner.EnableMeasurementEvents(instrument);
                }
            }
        };

        listener.SetMeasurementEventCallback<long>((_, count, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == "error.type" && tag.Value is string reason && reason is "queue_full" or "already_shutdown")
                {
                    Interlocked.Add(ref health.ExportFailures, count);
                    health.Emergency("otlp_queue_full");
                    break;
                }
            }
        });
        listener.Start();

        var processor = new BatchLogRecordExportProcessor(
            new ObservedLogExporter(options.Value, health),
            maxQueueSize: 2048,
            scheduledDelayMilliseconds: 1000,
            exporterTimeoutMilliseconds: 2000,
            maxExportBatchSize: 200);

        factory = LoggerFactory.Create(logging => logging.SetMinimumLevel(LogLevel.Trace).AddOpenTelemetry(o =>
        {
            o.IncludeScopes = true;
            o.IncludeFormattedMessage = true;
            o.SetResourceBuilder(DiagnosticRegistration.Resource(options.Value));
            o.AddProcessor(new CorrelationProcessor());
            o.AddProcessor(processor);
        }));

        logger = factory.CreateLogger("AiNexus.Sanitized");
    }

    /// <summary>
    /// 把已清洗的事件批次轉成 OpenTelemetry LogRecord 後匯出。
    /// </summary>
    public void Export(IReadOnlyList<DiagnosticEvent> events)
    {
        if (logger is null)
        {
            return;
        }

        foreach (var item in events)
        {
            try
            {
                DiagnosticRedactor.Normalize(item);
                using var scope = logger.BeginScope(new Dictionary<string, object?>
                {
                    ["LogId"] = item.LogId.ToString(),
                    ["IssueCode"] = item.IssueCode,
                    ["OriginalTimestamp"] = item.At.UtcDateTime,
                    ["OriginalTraceId"] = item.TraceId,
                    ["OriginalSpanId"] = item.SpanId,
                    ["Category"] = item.Category,
                    ["Properties"] = item.PropertiesJson,
                    ["ExceptionType"] = item.ExceptionType,
                    ["ExceptionDetail"] = item.ExceptionDetail,
                    ["RequestId"] = item.RequestId,
                    ["OperationId"] = item.OperationId?.ToString(),
                    ["JobId"] = item.JobId?.ToString(),
                    ["RunId"] = item.RunId?.ToString(),
                    ["Attempt"] = item.Attempt,
                    ["Method"] = item.Method,
                    ["Route"] = item.Route,
                    ["StatusCode"] = item.StatusCode,
                    ["DurationMs"] = item.DurationMs,
                    ["ExternalService"] = item.ExternalService,
                    ["ErrorCode"] = item.ErrorCode,
                    ["UserId"] = item.UserId?.ToString(),
                    ["Instance"] = item.Instance,
                    ["Environment"] = item.Environment
                });

                var rendered = DiagnosticMessage.Render(item, includeProperties: true);
                if (logger.IsEnabled(item.Level))
                {
                    logger.Log(item.Level, new EventId(item.EventId, item.EventName),
                        new[]
                        {
                            new KeyValuePair<string, object?>("{OriginalFormat}", item.MessageTemplate),
                            new KeyValuePair<string, object?>("RenderedMessage", rendered)
                        },
                        null,
                        (_, _) => rendered);
                }
            }
            catch (Exception)
            {
                Interlocked.Increment(ref health.ExportFailures);
                health.Emergency("otlp_export_failed");
            }
        }
    }

    private sealed class CorrelationProcessor : BaseProcessor<LogRecord>
    {
        public override void OnEnd(LogRecord record)
        {
            record.TraceId = default;
            record.SpanId = default;
            record.ForEachScope((scope, item) =>
            {
                foreach (var property in scope)
                {
                    if (property.Key == "OriginalTraceId" && property.Value is string trace && trace.Length == 32)
                    {
                        item.TraceId = ActivityTraceId.CreateFromString(trace);
                    }
                    else if (property.Key == "OriginalSpanId" && property.Value is string span && span.Length == 16)
                    {
                        item.SpanId = ActivitySpanId.CreateFromString(span);
                    }
                    else if (property.Key == "OriginalTimestamp" && property.Value is DateTime at)
                    {
                        item.Timestamp = at;
                    }
                }
            }, record);
        }
    }

    public void Dispose()
    {
        factory?.Dispose();
        listener?.Dispose();
    }

    private sealed class ObservedLogExporter(DiagnosticOptions options, DiagnosticHealth health) : BaseExporter<LogRecord>
    {
        private readonly OtlpLogExporter inner = new(DiagnosticRegistration.Otlp(new OtlpExporterOptions(), options, "logs"));

        public override ExportResult Export(in Batch<LogRecord> batch)
        {
            var result = inner.Export(batch);
            if (result == ExportResult.Failure)
            {
                Interlocked.Increment(ref health.ExportFailures);
                health.Emergency("otlp_export_failed");
            }
            else
            {
                health.Recovered("otlp");
            }
            return result;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

/// <summary>
/// 平台診斷日誌與 OpenTelemetry 遙測的註冊擴充。
/// </summary>
public static class DiagnosticRegistration
{
    /// <summary>
    /// 自訂與框架內建的計量表清單（路由耗時、Kestrel 連線、限流與授權結果、對外 HTTP、EF Core 查詢數、GC 與執行緒池）。
    /// 標籤絕不攜帶 SQL 全文、完整 URL、使用者或客戶端 IP；自動追蹤探針維持停用，以防外洩。
    /// </summary>
    public static readonly string[] Meters =
    [
        "AiNexus.Diagnostics", "AiNexus.Runtime",
        "Microsoft.AspNetCore.Hosting", "Microsoft.AspNetCore.Server.Kestrel", "Microsoft.AspNetCore.Routing",
        "Microsoft.AspNetCore.Diagnostics", "Microsoft.AspNetCore.RateLimiting", "Microsoft.AspNetCore.Authentication",
        "Microsoft.AspNetCore.Authorization", "System.Net.Http", "Microsoft.EntityFrameworkCore", "System.Runtime",
    ];

    /// <summary>
    /// 向 WebApplicationBuilder 註冊診斷緩衝、日誌提供者、期刊持久化、OTLP 匯出器與監控計量。
    /// </summary>
    public static void AddPlatformDiagnostics(this WebApplicationBuilder builder)
    {
        builder.Services.AddSettings<DiagnosticOptions, DiagnosticOptionsValidator>(DiagnosticOptions.Section);
        builder.Services.AddSingleton<DiagnosticHealth>();
        builder.Services.AddSingleton<DiagnosticBuffer>();
        builder.Services.AddSingleton<DiagnosticLoggerProvider>();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<ILoggerProvider>(sp => sp.GetRequiredService<DiagnosticLoggerProvider>());

        // 只排除 None；其餘層級由集中的最低層級政策決定，不能讓類別預設值悄悄覆寫。
        builder.Logging.AddFilter<DiagnosticLoggerProvider>((_, level) => level != LogLevel.None);
        builder.Services.AddSingleton<DiagnosticJournal>();
        builder.Services.AddSingleton<IDiagnosticJournal>(sp => sp.GetRequiredService<DiagnosticJournal>());
        builder.Services.AddSingleton<DiagnosticExporter>();
        builder.Services.AddSingleton<Issues>();

        // 最先註冊，因此最後才停止，確保業務 Worker 發出的終態事件都能寫入。
        builder.Services.AddHostedService<DiagnosticWorker>();

        var configuration = DiagnosticOptions.Read(builder.Configuration);
        var telemetry = builder.Services.AddOpenTelemetry();
        telemetry.WithTracing(t =>
        {
            t.SetResourceBuilder(Resource(configuration)).AddSource(DiagnosticTrace.SourceName).SetSampler(new AlwaysOnSampler());
            if (configuration.OtlpEnabled) t.AddOtlpExporter(o => Otlp(o, configuration, "traces"));
        });

        telemetry.WithMetrics(m =>
        {
            m.SetResourceBuilder(Resource(configuration)).AddMeter(Meters);
            if (configuration.OtlpEnabled) m.AddOtlpExporter(o => Otlp(o, configuration, "metrics"));
        });
    }

    /// <summary>日誌、追蹤與計量共用的服務資源描述；名稱與版本先脫敏，不讓設定值原樣外送。</summary>
    internal static ResourceBuilder Resource(DiagnosticOptions options) => ResourceBuilder.CreateEmpty().AddService(
        DiagnosticRedactor.Text(options.ServiceName, 80),
        serviceVersion: DiagnosticRedactor.Text(DiagnosticLoggerProvider.Version, 80));

    /// <summary>三種訊號共用的 OTLP/HTTP 匯出設定：<paramref name="signal"/> 為 <c>logs</c>、<c>traces</c> 或 <c>metrics</c>，逾時固定 2 秒。</summary>
    internal static OtlpExporterOptions Otlp(OtlpExporterOptions exporter, DiagnosticOptions options, string signal)
    {
        exporter.Endpoint = new Uri(options.OtlpEndpoint.TrimEnd('/') + "/v1/" + signal);
        exporter.Protocol = OtlpExportProtocol.HttpProtobuf;
        exporter.TimeoutMilliseconds = 2000;
        return exporter;
    }
}
