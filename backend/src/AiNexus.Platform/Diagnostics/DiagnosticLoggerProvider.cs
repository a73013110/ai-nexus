using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace AiNexus.Platform.Diagnostics;

public sealed class DiagnosticLoggerProvider(DiagnosticBuffer buffer, IOptions<DiagnosticOptions> options, IHostEnvironment environment, IHttpContextAccessor? http = null) : ILoggerProvider, ISupportExternalScope
{
    private readonly DiagnosticBuffer buffer = buffer;
    private readonly IOptions<DiagnosticOptions> options = options;
    private readonly IHostEnvironment environment = environment;
    private readonly IHttpContextAccessor? http = http;
    private IExternalScopeProvider scopes = new LoggerExternalScopeProvider();
    public string Instance { get; } = System.Environment.MachineName + "-" + System.Environment.ProcessId + "-" + Guid.NewGuid().ToString("N")[..8];
    public static string Version { get; } = typeof(DiagnosticLoggerProvider).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
    public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);
    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => scopes = scopeProvider;
    public void Dispose() { }
    private sealed class Logger(DiagnosticLoggerProvider provider, string category) : ILogger
    {
        public bool IsEnabled(LogLevel level) => level != LogLevel.None && (level >= LogLevel.Error || level >= (category.StartsWith("Microsoft.", StringComparison.Ordinal) || category.StartsWith("System.", StringComparison.Ordinal) ? provider.options.Value.FrameworkMinimumLevel : provider.options.Value.MinimumLevel));
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => provider.scopes.Push(state);
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if ((!IsEnabled(level) && eventId.Id != DiagnosticEvents.Rejection) || DiagnosticSuppression.Active) return;
            var values = new List<KeyValuePair<string, object?>>();
            provider.scopes.ForEachScope((scope, list) => { if (scope is IEnumerable<KeyValuePair<string, object?>> items) list.AddRange(items.Take(64)); }, values);
            string template = "Unstructured event [text omitted]";
            if (state is IEnumerable<KeyValuePair<string, object?>> fields)
            {
                foreach (var pair in fields.Take(64))
                    if (pair.Key == "{OriginalFormat}" && pair.Value is string t) template = t;
                    else values.Add(pair);
            }
            var properties = DiagnosticRedactor.Properties(values);
            string? S(string key) => properties.GetValueOrDefault(key)?.ToString();
            Guid? G(string key) => Guid.TryParse(S(key), out var id) ? id : null;
            int? I(string key) => int.TryParse(S(key), out var n) ? n : null;
            double? D(string key) => double.TryParse(S(key), System.Globalization.CultureInfo.InvariantCulture, out var n) && double.IsFinite(n) ? n : null;
            template = DiagnosticRedactor.Text(template, 2048);
            var stableId = eventId.Id != 0 ? eventId.Id : BitConverter.ToInt32(SHA256.HashData(Encoding.UTF8.GetBytes(category + ":" + template)), 0) & int.MaxValue;
            // Hosting spans outside our middleware may have a caller-supplied traceparent; never treat them as trusted correlation.
            var activity = Activity.Current?.Source.Name == DiagnosticTrace.SourceName || Activity.Current?.GetTagItem("nexus.trusted") is true ? Activity.Current : null;
            var userId = G("UserId");
            if (userId is null && provider.http?.HttpContext is { User.Identity.IsAuthenticated: true } context)
                userId = context.RequestServices.GetService<AiNexus.Platform.Security.IRequestUser>()?.ResolvedId;
            var accepted = provider.buffer.Enqueue(new()
            {
                Level = level, Category = DiagnosticRedactor.Text(category, 180), EventId = stableId,
                EventName = DiagnosticRedactor.Text(eventId.Name ?? "event." + stableId, 100), MessageTemplate = template,
                PropertiesJson = DiagnosticRedactor.Json(properties), Service = DiagnosticRedactor.Text(provider.options.Value.ServiceName, 80),
                Environment = DiagnosticRedactor.Text(provider.environment.EnvironmentName, 32), Version = DiagnosticRedactor.Text(Version, 80), Instance = provider.Instance,
                IssueCode = S("IssueCode") ?? (level >= LogLevel.Warning ? Issues.NewCode() : null),
                TraceId = activity?.TraceId.ToHexString(), SpanId = activity?.SpanId.ToHexString(), RequestId = S("RequestId"),
                OperationId = G("OperationId"), JobId = G("JobId"), RunId = G("RunId"), UserId = userId, Attempt = I("Attempt"),
                Method = S("Method"), Route = S("Route"), StatusCode = I("StatusCode"), DurationMs = D("DurationMs"),
                ExternalService = S("ExternalService") ?? S("Provider"), ErrorCode = S("ErrorCode") ?? S("Code"),
                ExceptionType = exception is null ? S("ErrorType") : DiagnosticRedactor.Text(exception.GetType().FullName, 180),
                ExceptionDetail = exception is null ? null : DiagnosticRedactor.Exception(exception), UntrustedClient = S("UntrustedClient") == "True"
            });
            foreach (var receipt in values.Where(x => x.Key == "NexusDelivery").Select(x => x.Value).OfType<DiagnosticDelivery>()) receipt.Accepted = accepted;
        }
    }
}
