using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Routing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Security;

namespace AiNexus.Platform.Diagnostics;

public static class DiagnosticEvents
{
    public static readonly EventId Request = new(1000, "http.completed"), Failure = new(1001, "operation.failed"), Rejection = new(1002, "http.rejected"),
        Degraded = new(2001, "retrieval.degraded"), JobStarted = new(3000, "job.started"), JobFinished = new(3001, "job.finished"),
        RunStarted = new(3100, "generation.started"), RunFinished = new(3101, "generation.finished"), Client = new(4001, "client.unhandled"),
        Started = new(5000, "service.started"), Stopping = new(5001, "service.stopping"), Configuration = new(5002, "service.startup.failed");
}

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
public sealed record SafeProblemDetails(string Type, string Title, int Status, string Code, string IssueCode);
public static class SafeErrorMetadata
{
    public static RouteGroupBuilder WithSafeErrors(this RouteGroupBuilder group)
    {
        foreach (var status in new[] { 400, 401, 403, 404, 405, 409, 413, 415, 429, 500, 502, 503, 504 })
            group.WithMetadata(new Microsoft.AspNetCore.Http.ProducesResponseTypeMetadata(status, typeof(SafeProblemDetails), ["application/problem+json"]));
        return group;
    }
}

public sealed class Issues(ILogger<Issues> logger, ILoggerFactory? factory = null)
{
    private const string Key = "AiNexus.IssueCode";
    public static string NewCode() => "NX-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
    public static bool ValidCode(string? code) => code is { Length: 35 } && code.StartsWith("NX-", StringComparison.Ordinal) && code[3..].All(char.IsAsciiHexDigit);
    public static string Message(string? issue) => "操作未完成，請聯絡管理員。查證代碼：" + (ValidCode(issue) ? issue : "無法取得");
    public string Report(Exception exception, string code, LogLevel level = LogLevel.Error, EventId? eventId = null)
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
            target.Log(level, eventId ?? DiagnosticEvents.Failure, exception, "Operation failed with {ErrorCode}; issue {IssueCode}.", code, issue);
            Activity.Current?.SetStatus(ActivityStatusCode.Error, DiagnosticRedactor.Text(code, 80));
            return issue;
        }
    }
    public PublicProblem Problem(Exception exception)
    {
        var status = exception is ApiException api ? api.Status : exception is BadHttpRequestException bad ? bad.StatusCode : exception is AntiforgeryValidationException ? 403 : 503;
        var code = exception is ApiException a ? a.Code : status == 403 ? "csrf_invalid" : status == 413 ? "request_too_large" : status == 400 ? "invalid_request" : "service_unavailable";
        var issue = Report(exception, code, status >= 500 ? LogLevel.Error : LogLevel.Information, status >= 500 ? DiagnosticEvents.Failure : DiagnosticEvents.Rejection);
        return new(status, code, status < 500 ? PublicErrorCatalog.Message(code, status) : Message(issue), issue);
    }
    public static Task WriteAsync(HttpContext http, PublicProblem problem) => Results.Json(new SafeProblemDetails("urn:ai-nexus:problem:" + problem.Code, problem.Title, problem.Status, problem.Code, problem.IssueCode),
        statusCode: problem.Status, contentType: "application/problem+json").ExecuteAsync(http);
}

public sealed class DiagnosticRequestMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext http, Issues issues, ILogger<DiagnosticRequestMiddleware> logger)
    {
        WebSecurity.Headers(http);
        var previous = Activity.Current;
        Activity.Current = null;
        using var activity = DiagnosticTrace.Start("http.request", kind: ActivityKind.Server);
        var watch = Stopwatch.StartNew();
        var operation = Guid.NewGuid(); http.TraceIdentifier = Guid.NewGuid().ToString("N");
        activity.SetTag("operation.id", operation.ToString());
        using var scope = logger.BeginScope(new Dictionary<string, object?> { ["RequestId"] = http.TraceIdentifier, ["OperationId"] = operation });
        try
        {
            await next(http);
            // Includes framework binding/authorization failures that do not throw.
            if (http.Response.StatusCode >= 400 && !http.Response.HasStarted && http.Response.ContentLength is null or 0)
            {
                var code = http.Response.StatusCode switch { 401 => "authentication_required", 403 => "access_denied", 404 => "not_found", 429 => "rate_limited", _ => "invalid_request" };
                await Issues.WriteAsync(http, issues.Problem(new ApiException(http.Response.StatusCode, code, "")));
            }
        }
        catch (OperationCanceledException) when (http.RequestAborted.IsCancellationRequested) { logger.LogInformation("Request cancelled by caller."); }
        catch (Exception exception)
        {
            using var failureScope = logger.BeginScope(new Dictionary<string, object?> { ["Method"] = http.Request.Method,
                ["Route"] = (http.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText, ["StatusCode"] = exception is ApiException api ? api.Status : 503 });
            var problem = issues.Problem(exception);
            if (!http.Response.HasStarted) { http.Response.Clear(); WebSecurity.Headers(http); await Issues.WriteAsync(http, problem); }
            else if (http.Response.ContentType?.StartsWith("text/event-stream", StringComparison.OrdinalIgnoreCase) == true && !http.RequestAborted.IsCancellationRequested)
            {
                await http.Response.WriteAsync("event: error\ndata: " + System.Text.Json.JsonSerializer.Serialize(new { code = problem.Code, message = problem.Status >= 500 ? Issues.Message(problem.IssueCode) : problem.Title, issueCode = problem.IssueCode }) + "\n\n", http.RequestAborted);
            }
            else http.Abort();
        }
        finally
        {
            var route = (http.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
            activity.SetTag("http.route", route); activity.SetTag("http.request.method", http.Request.Method); activity.SetTag("http.response.status_code", http.Response.StatusCode);
            var user = http.RequestServices.GetService<AiNexus.Platform.Security.IRequestUser>()?.ResolvedId;
            using var completionScope = logger.BeginScope(new Dictionary<string, object?> { ["Method"] = http.Request.Method, ["Route"] = route,
                ["StatusCode"] = http.Response.StatusCode, ["DurationMs"] = watch.Elapsed.TotalMilliseconds, ["UserId"] = user });
            logger.LogInformation(DiagnosticEvents.Request, "HTTP request completed with {StatusCode} in {DurationMs} ms.", http.Response.StatusCode, watch.Elapsed.TotalMilliseconds);
            Activity.Current = previous;
        }
    }
}
