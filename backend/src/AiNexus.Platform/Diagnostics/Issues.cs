using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Antiforgery;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Security;

namespace AiNexus.Platform.Diagnostics;

/// <summary>
/// Ids of the core events that code outside their <c>[LoggerMessage]</c> declaration refers to. Every event id and
/// name is listed in docs/LOG_EVENTS.md.
/// </summary>
public static class DiagnosticEvents
{
    public const int Request = 1000, Failure = 1001, Rejection = 1002, Degraded = 2001, Configuration = 5002;
    public const string ConfigurationName = "service.startup.failed";
}

/// <summary>Successful observability reads are already audited; do not feed them back into their own log list.</summary>
public sealed class SuppressSuccessfulRequestLog;

public static class DiagnosticTrace
{
    public const string SourceName = "AiNexus";
    public static readonly ActivitySource Source = new(SourceName, "1.0");
    public static Activity Start(string name, string? traceId = null, string? spanId = null, ActivityKind kind = ActivityKind.Internal)
    {
        ActivityContext parent = default;
        if (traceId?.Length == 32 && spanId?.Length == 16 && ActivityContext.TryParse("00-" + traceId + "-" + spanId + "-01", null, out var parsed)) parent = parsed;
        // Explicit parent, including default, prevents accidental inheritance from the request/queue caller.
        var activity = Source.StartActivity(name, kind, parent) ?? new Activity(name).SetIdFormat(ActivityIdFormat.W3C)
            .SetParentId(parent == default ? ActivityTraceId.CreateRandom() : parent.TraceId, parent == default ? ActivitySpanId.CreateRandom() : parent.SpanId, ActivityTraceFlags.Recorded).Start();
        activity.SetTag("nexus.trusted", true); return activity;
    }
}

public sealed record PublicProblem(int Status, string Code, string Title, string IssueCode);
public sealed record SafeProblemDetails(string Type, string Title, int Status, string Code, string IssueCode,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyDictionary<string, string[]>? Errors = null);
public static class SafeErrorMetadata
{
    public static RouteGroupBuilder WithSafeErrors(this RouteGroupBuilder group)
    {
        foreach (var status in new[] { 400, 401, 403, 404, 405, 409, 413, 415, 429, 500, 502, 503, 504 })
            group.WithMetadata(new Microsoft.AspNetCore.Http.ProducesResponseTypeMetadata(status, typeof(SafeProblemDetails), ["application/problem+json"]));
        return group;
    }
}

public sealed partial class Issues(ILogger<Issues> logger, ILoggerFactory? factory = null)
{
    private const string Key = "AiNexus.IssueCode";
    public static string NewCode() => "NX-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
    public static bool ValidCode(string? code) => code is { Length: 35 } && code.StartsWith("NX-", StringComparison.Ordinal) && code[3..].All(char.IsAsciiHexDigit);
    public static string Message(string? issue) => "操作未完成，請聯絡管理員。查證代碼：" + (ValidCode(issue) ? issue : "無法取得");
    public string Report(Exception exception, string code, LogLevel level = LogLevel.Error) => Report(exception, code, level, rejection: false);

    /// <summary>Records an expected failure that has no exception, such as a background job that returned an <see cref="Error"/>.</summary>
    public string Report(Error error) => Record(Problems.Status(error.Kind), error.Code);

    private string Report(Exception exception, string code, LogLevel level, bool rejection)
    {
        // An exception crossing layers is recorded once. Each distinct exception receives its own opaque code.
        lock (exception.Data)
        {
            if (exception.Data[Key] is string existing) return existing;
            var issue = NewCode(); exception.Data[Key] = issue;
            var origin = new StackTrace(exception, false).GetFrames()?.Select(frame => frame.GetMethod()?.DeclaringType?.FullName).FirstOrDefault(name => name?.StartsWith("AiNexus.Features.", StringComparison.Ordinal) == true);
            var category = origin is null ? null : string.Join('.', origin.Split('.').Take(3));
            var target = category is null ? logger : factory?.CreateLogger(category) ?? logger;
            using var scope = target.BeginScope(new Dictionary<string, object?> { ["IssueCode"] = issue, ["ErrorCode"] = code });
            if (rejection) LogRejection(target, level, exception, code, issue);
            else LogFailure(target, level, exception, code, issue);
            Activity.Current?.SetStatus(ActivityStatusCode.Error, DiagnosticRedactor.Text(code, 80));
            return issue;
        }
    }

    /// <summary>The public status and code of an exception that escaped a request.</summary>
    public static (int Status, string Code) Classify(Exception exception) => exception switch
    {
        ExternalServiceException external => (Problems.Status(external.Error.Kind), external.Error.Code),
        BadHttpRequestException bad => (bad.StatusCode, bad.StatusCode switch { 413 => "request_too_large", 400 => "invalid_request", _ => "service_unavailable" }),
        AntiforgeryValidationException => (403, "csrf_invalid"),
        _ => (503, "service_unavailable"),
    };

    public PublicProblem Problem(Exception exception)
    {
        var (status, code) = Classify(exception);
        var issue = Report(exception, code, status >= 500 ? LogLevel.Error : LogLevel.Information, rejection: status < 500);
        return Public(status, code, issue);
    }

    /// <summary>A failure written without an exception: an <see cref="Error"/> result, validation, or an empty framework status.</summary>
    public PublicProblem Problem(int status, string code, string? recordedIssue = null) => Public(status, code, recordedIssue ?? Record(status, code));

    private string Record(int status, string code)
    {
        var issue = NewCode();
        using var scope = logger.BeginScope(new Dictionary<string, object?> { ["IssueCode"] = issue, ["ErrorCode"] = code });
        if (status < 500) LogRejection(logger, LogLevel.Information, null, code, issue);
        else LogFailure(logger, LogLevel.Error, null, code, issue);
        Activity.Current?.SetStatus(ActivityStatusCode.Error, DiagnosticRedactor.Text(code, 80));
        return issue;
    }

    private static PublicProblem Public(int status, string code, string issue)
        => new(status, code, status < 500 ? PublicErrorCatalog.Message(code, status) : Message(issue), issue);

    [LoggerMessage(EventId = DiagnosticEvents.Failure, EventName = "operation.failed", Message = "Operation failed with {ErrorCode}; issue {IssueCode}.")]
    private static partial void LogFailure(ILogger logger, LogLevel level, Exception? exception, string errorCode, string issueCode);

    // The diagnostic provider keeps rejections below its minimum level, so the generated enabled check must not drop them.
    [LoggerMessage(EventId = DiagnosticEvents.Rejection, EventName = "http.rejected", Message = "Operation failed with {ErrorCode}; issue {IssueCode}.", SkipEnabledCheck = true)]
    private static partial void LogRejection(ILogger logger, LogLevel level, Exception? exception, string errorCode, string issueCode);

    // Only Problems' writer calls this; everything else goes through IProblemDetailsService.
    /// <summary>The problem as the final <c>error</c> event of a server-sent event stream that has already started.</summary>
    internal static Task WriteEventAsync(HttpContext http, PublicProblem problem)
        => http.Response.WriteAsync("event: error\ndata: " + System.Text.Json.JsonSerializer.Serialize(new { status = problem.Status, code = problem.Code, message = problem.Status >= 500 ? Message(problem.IssueCode) : problem.Title, issueCode = problem.IssueCode }) + "\n\n", http.RequestAborted);

    internal static Task WriteAsync(HttpContext http, PublicProblem problem, IReadOnlyDictionary<string, string[]>? errors = null)
        => Results.Json(new SafeProblemDetails("urn:ai-nexus:problem:" + problem.Code, problem.Title, problem.Status, problem.Code, problem.IssueCode, errors),
            statusCode: problem.Status, contentType: "application/problem+json").ExecuteAsync(http);
}

public sealed partial class DiagnosticRequestMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext http, Issues issues, ILogger<DiagnosticRequestMiddleware> logger)
    {
        WebSecurity.Headers(http);
        var previous = Activity.Current;
        Activity.Current = null;
        using var activity = DiagnosticTrace.Start("http.request", kind: ActivityKind.Server);
        http.Items["Nexus.TraceId"] = activity.TraceId.ToHexString();
        var watch = Stopwatch.StartNew();
        var operation = Guid.NewGuid(); http.TraceIdentifier = Guid.NewGuid().ToString("N");
        activity.SetTag("operation.id", operation.ToString());
        var address = http.Connection.RemoteIpAddress;
        if (address?.IsIPv4MappedToIPv6 == true) address = address.MapToIPv4();
        // Connection metadata may have been resolved by the host's trusted proxy middleware.
        // Do not parse caller-controlled X-Forwarded-For here.
        using var scope = logger.BeginScope(new Dictionary<string, object?> {
            ["RequestId"] = http.TraceIdentifier, ["OperationId"] = operation, ["Method"] = http.Request.Method,
            ["ClientAddress"] = address?.ToString(), ["UserAgent"] = DiagnosticRedactor.Text(http.Request.Headers.UserAgent.ToString(), 240),
            ["RequestProtocol"] = http.Request.Protocol, ["RequestScheme"] = http.Request.Scheme
        });
        var outcome = "completed";
        try
        {
            // Empty framework failures (challenge, forbid, unmatched route) get their body from UseStatusCodePages.
            await next(http);
        }
        catch (OperationCanceledException) when (http.RequestAborted.IsCancellationRequested) { outcome = "cancelled"; }
        catch (Exception exception)
        {
            outcome = "failed";
            using var failureScope = logger.BeginScope(new Dictionary<string, object?> { ["Method"] = http.Request.Method,
                ["Route"] = (http.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText, ["StatusCode"] = Issues.Classify(exception).Status });
            if (!http.Response.HasStarted) { http.Response.Clear(); WebSecurity.Headers(http); await Problems.WriteAsync(http, exception); }
            else if (http.Response.ContentType?.StartsWith("text/event-stream", StringComparison.OrdinalIgnoreCase) == true && !http.RequestAborted.IsCancellationRequested)
                await Issues.WriteEventAsync(http, issues.Problem(exception));
            else http.Abort();
        }
        finally
        {
            watch.Stop();
            var duration = Math.Round(watch.Elapsed.TotalMilliseconds, 3);
            var route = (http.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
            activity.SetTag("http.route", route); activity.SetTag("http.request.method", http.Request.Method); activity.SetTag("http.response.status_code", http.Response.StatusCode);
            activity.SetTag("client.address", address?.ToString()); activity.SetTag("network.protocol.version", http.Request.Protocol.Replace("HTTP/", "", StringComparison.OrdinalIgnoreCase));
            if (outcome == "failed" || http.Response.StatusCode >= 500) activity.SetStatus(ActivityStatusCode.Error);
            var user = http.RequestServices.GetService<AiNexus.Platform.Security.IRequestUser>()?.ResolvedId;
            using var completionScope = logger.BeginScope(new Dictionary<string, object?> { ["Method"] = http.Request.Method, ["Route"] = route,
                ["StatusCode"] = http.Response.StatusCode, ["DurationMs"] = duration, ["UserId"] = user,
                ["RequestOutcome"] = outcome, ["RequestAborted"] = http.RequestAborted.IsCancellationRequested, ["ResponseStarted"] = http.Response.HasStarted });
            if (outcome != "completed" || http.Response.StatusCode >= 400 ||
                http.GetEndpoint()?.Metadata.GetMetadata<SuppressSuccessfulRequestLog>() is null)
                LogRequestFinished(logger, http.Response.StatusCode, duration);
            Activity.Current = previous;
        }
    }

    [LoggerMessage(EventId = DiagnosticEvents.Request, EventName = "http.completed", Level = LogLevel.Information, Message = "HTTP request finished with {StatusCode} in {DurationMs:0.###} ms.")]
    private static partial void LogRequestFinished(ILogger logger, int statusCode, double durationMs);
}
