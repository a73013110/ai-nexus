using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace AiNexus.Platform.Diagnostics;

/// <summary>
/// 攔截所有 ILogger 事件，轉成診斷事件後放進記憶體緩衝區。
/// 屬性在入列前就完成脫敏，緩衝區與之後的輸出都只會看到脫敏後的值。
/// </summary>
public sealed class DiagnosticLoggerProvider(
    DiagnosticBuffer buffer,
    IOptions<DiagnosticOptions> options,
    IHostEnvironment environment,
    IHttpContextAccessor? http = null,
    TimeProvider? clock = null) : ILoggerProvider, ISupportExternalScope
{
    private readonly DiagnosticBuffer buffer = buffer;
    private readonly IOptions<DiagnosticOptions> options = options;
    private readonly IHostEnvironment environment = environment;
    private readonly IHttpContextAccessor? http = http;
    private readonly TimeProvider clock = clock ?? TimeProvider.System;
    private IExternalScopeProvider scopes = new LoggerExternalScopeProvider();

    public string Instance { get; } =
        System.Environment.MachineName + "-" + System.Environment.ProcessId + "-" + Guid.NewGuid().ToString("N")[..8];

    public static string Version { get; } =
        typeof(DiagnosticLoggerProvider).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "unknown";

    public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => scopes = scopeProvider;

    public void Dispose() { }

    private sealed class Logger(DiagnosticLoggerProvider provider, string category) : ILogger
    {
        public bool IsEnabled(LogLevel level)
        {
            if (level == LogLevel.None)
            {
                return false;
            }

            // 框架與系統元件常有大量低階日誌，改用獨立的門檻，避免淹沒應用程式自己的事件。
            var minimum = IsFrameworkCategory()
                ? provider.options.Value.FrameworkMinimumLevel
                : provider.options.Value.MinimumLevel;
            return level >= LogLevel.Error || level >= minimum;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => provider.scopes.Push(state);

        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            // 拒絕事件即使低於門檻也要記錄，否則限流與拒絕的證據會消失。
            if ((!IsEnabled(level) && eventId.Id != DiagnosticEvents.Rejection) || DiagnosticSuppression.Active)
            {
                return;
            }

            var values = new List<KeyValuePair<string, object?>>();
            provider.scopes.ForEachScope((scope, list) =>
            {
                if (scope is IEnumerable<KeyValuePair<string, object?>> items)
                {
                    list.AddRange(items.Take(64));
                }
            }, values);

            string template = "Unstructured event [text omitted]";
            if (state is IEnumerable<KeyValuePair<string, object?>> fields)
            {
                foreach (var pair in fields.Take(64))
                {
                    if (pair.Key == "{OriginalFormat}" && pair.Value is string t)
                    {
                        template = t;
                    }
                    else
                    {
                        values.Add(pair);
                    }
                }
            }

            var properties = DiagnosticRedactor.Properties(values);
            string? StringOf(string key) => properties.GetValueOrDefault(key)?.ToString();
            Guid? GuidOf(string key) => Guid.TryParse(StringOf(key), out var id) ? id : null;
            int? IntOf(string key) => int.TryParse(StringOf(key), out var n) ? n : null;
            double? DoubleOf(string key) =>
                double.TryParse(StringOf(key), System.Globalization.CultureInfo.InvariantCulture, out var n) && double.IsFinite(n)
                    ? n
                    : null;

            template = DiagnosticRedactor.Text(template, 2048);
            var stableId = eventId.Id != 0
                ? eventId.Id
                : BitConverter.ToInt32(SHA256.HashData(Encoding.UTF8.GetBytes(category + ":" + template)), 0) & int.MaxValue;

            // 只信任本程式自己的 Activity 來源，或明確標記為可信的 Activity。
            // 其他 Span（例如主機層）可能帶著呼叫端送來的 traceparent，直接沿用會讓外部請求偽造追蹤關聯，所以這裡寧可不附上。
            var activity = Activity.Current?.Source.Name == DiagnosticTrace.SourceName || Activity.Current?.GetTagItem("nexus.trusted") is true
                ? Activity.Current
                : null;

            var userId = GuidOf("UserId");
            if (userId is null && provider.http?.HttpContext is { User.Identity.IsAuthenticated: true } context)
            {
                userId = context.RequestServices.GetService<AiNexus.Platform.Security.IRequestUser>()?.ResolvedId;
            }

            var accepted = provider.buffer.Enqueue(new()
            {
                At = provider.clock.GetUtcNow(),
                Level = level,
                Category = DiagnosticRedactor.Text(category, 180),
                EventId = stableId,
                EventName = DiagnosticRedactor.Text(eventId.Name ?? "event." + stableId, 100),
                MessageTemplate = template,
                PropertiesJson = DiagnosticRedactor.Json(properties),
                Service = DiagnosticRedactor.Text(provider.options.Value.ServiceName, 80),
                Environment = DiagnosticRedactor.Text(provider.environment.EnvironmentName, 32),
                Version = DiagnosticRedactor.Text(Version, 80),
                Instance = provider.Instance,
                IssueCode = StringOf("IssueCode") ?? (level >= LogLevel.Warning ? Issues.NewCode() : null),
                TraceId = activity?.TraceId.ToHexString(),
                SpanId = activity?.SpanId.ToHexString(),
                RequestId = StringOf("RequestId"),
                OperationId = GuidOf("OperationId"),
                JobId = GuidOf("JobId"),
                RunId = GuidOf("RunId"),
                UserId = userId,
                Attempt = IntOf("Attempt"),
                Method = StringOf("Method"),
                Route = StringOf("Route"),
                StatusCode = IntOf("StatusCode"),
                DurationMs = DoubleOf("DurationMs"),
                ExternalService = StringOf("ExternalService") ?? StringOf("Provider"),
                ErrorCode = StringOf("ErrorCode") ?? StringOf("Code"),
                ExceptionType = exception is null
                    ? StringOf("ErrorType")
                    : DiagnosticRedactor.Text(exception.GetType().FullName, 180),
                ExceptionDetail = exception is null ? null : DiagnosticRedactor.Exception(exception),
                UntrustedClient = StringOf("UntrustedClient") == "True"
            });

            foreach (var receipt in values.Where(x => x.Key == "NexusDelivery").Select(x => x.Value).OfType<DiagnosticDelivery>())
            {
                receipt.Accepted = accepted;
            }
        }

        private bool IsFrameworkCategory() =>
            category.StartsWith("Microsoft.", StringComparison.Ordinal) || category.StartsWith("System.", StringComparison.Ordinal);
    }
}
