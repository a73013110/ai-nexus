using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Antiforgery;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Security;

namespace AiNexus.Platform.Diagnostics;

/// <summary>
/// 核心診斷事件的 ID 常數，供 [LoggerMessage] 以外的程式碼引用。每個事件 ID 與名稱都登錄在 docs/architecture/log-events.md。
/// </summary>
public static class DiagnosticEvents
{
    public const int Request = 1000, Failure = 1001, Rejection = 1002, Degraded = 2001, Configuration = 5002;
    public const string ConfigurationName = "service.startup.failed";
}

/// <summary>
/// 標記成功時不需寫入請求日誌的可觀測性端點。這類查詢本身已有稽核紀錄，再寫一次只會自我循環。
/// </summary>
public sealed class SuppressSuccessfulRequestLog;

/// <summary>
/// 受信任的 W3C 分散式追蹤工廠。確保內部 Trace 不會被外部、不受信任的 HTTP 標頭汙染。
/// </summary>
public static class DiagnosticTrace
{
    public const string SourceName = "AiNexus";
    public static readonly ActivitySource Source = new(SourceName, "1.0");

    /// <summary>
    /// 啟動一個新的受信任 Activity。父內容由呼叫端明確指定（未指定時為新的根），
    /// 避免意外承接不受信任的請求或佇列呼叫端的追蹤。
    /// </summary>
    public static Activity Start(string name, string? traceId = null, string? spanId = null, ActivityKind kind = ActivityKind.Internal)
    {
        ActivityContext parent = default;
        if (traceId?.Length == 32 && spanId?.Length == 16 && ActivityContext.TryParse("00-" + traceId + "-" + spanId + "-01", null, out var parsed))
        {
            parent = parsed;
        }

        var activity = Source.StartActivity(name, kind, parent)
            ?? new Activity(name).SetIdFormat(ActivityIdFormat.W3C)
                .SetParentId(parent == default ? ActivityTraceId.CreateRandom() : parent.TraceId, parent == default ? ActivitySpanId.CreateRandom() : parent.SpanId, ActivityTraceFlags.Recorded)
                .Start();

        activity.SetTag("diagnostic.trusted", true);
        return activity;
    }
}

/// <summary>
/// 對外公開的問題結果。
/// </summary>
public sealed record PublicProblem(int Status, string Code, string Title, string IssueCode);

/// <summary>
/// 符合 RFC 7807 且已脫敏的 ProblemDetails。
/// </summary>
public sealed record SafeProblemDetails(
    string Type,
    string Title,
    int Status,
    string Code,
    string IssueCode,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyDictionary<string, string[]>? Errors = null);

/// <summary>
/// 路由群組的錯誤回應中繼資料擴充，讓 OpenAPI 文件列出常見錯誤狀態碼對應的 <see cref="SafeProblemDetails"/>。
/// </summary>
public static class SafeErrorMetadata
{
    public static RouteGroupBuilder WithSafeErrors(this RouteGroupBuilder group)
    {
        foreach (var status in new[] { 400, 401, 403, 404, 405, 409, 413, 415, 429, 500, 502, 503, 504 })
        {
            group.WithMetadata(new Microsoft.AspNetCore.Http.ProducesResponseTypeMetadata(status, typeof(SafeProblemDetails), ["application/problem+json"]));
        }
        return group;
    }
}

/// <summary>
/// 平台錯誤與查證代碼（IssueCode）的集中管理器。確保同一例外跨層時只記錄一次，
/// 產生全域唯一、不透明的 NX- 查證代碼，並對應到對外安全的錯誤提示。
/// </summary>
public sealed partial class Issues(ILogger<Issues> logger, ILoggerFactory? factory = null)
{
    private const string Key = "AiNexus.IssueCode";

    public static string NewCode() => "NX-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
    public static bool ValidCode(string? code) => code is { Length: 35 } && code.StartsWith("NX-", StringComparison.Ordinal) && code[3..].All(char.IsAsciiHexDigit);
    public static string Message(string? issue) => "操作未完成，請聯絡管理員。查證代碼：" + (ValidCode(issue) ? issue : "無法取得");

    /// <summary>
    /// 沒有 HTTP 回應的失敗事件（如背景任務）所用的持久化文字。預期內的錯誤保留審查過的繁中提示。
    /// </summary>
    public static string Message(Error error, string issue)
        => Problems.Status(error.Kind) is var status && status < 500 ? PublicErrorCatalog.Message(error.Code, status) + " 查證代碼：" + issue : Message(issue);

    public string Report(Exception exception, string code, LogLevel level = LogLevel.Error) => Report(exception, code, level, rejection: false);

    /// <summary>
    /// 記錄無例外的預期失敗，例如回傳 <see cref="Error"/> 的背景任務。
    /// </summary>
    public string Report(Error error) => Record(Problems.Status(error.Kind), error.Code);

    private string Report(Exception exception, string code, LogLevel level, bool rejection)
    {
        // 跨層傳遞的例外只記錄一次；每個例外取得獨一無二的不透明代碼。
        lock (exception.Data)
        {
            if (exception.Data[Key] is string existing)
            {
                return existing;
            }

            var issue = NewCode();
            exception.Data[Key] = issue;

            var origin = new StackTrace(exception, false).GetFrames()?
                .Select(frame => frame.GetMethod()?.DeclaringType?.FullName)
                .FirstOrDefault(name => name?.StartsWith("AiNexus.Features.", StringComparison.Ordinal) == true);

            // 分類取到模組為止（AiNexus.Features.<區>.<模組>），同一模組的失敗歸在同一個 logger 分類。
            var category = origin is null ? null : string.Join('.', origin.Split('.').Take(4));
            Write(category is null ? logger : factory?.CreateLogger(category) ?? logger, level, exception, code, issue, rejection);
            return issue;
        }
    }

    /// <summary>
    /// 判斷逃出請求範圍的例外，應回傳的公開 HTTP 狀態碼與錯誤代碼。
    /// </summary>
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

    /// <summary>
    /// 沒有例外時寫入的公開問題，例如 <see cref="Error"/> 結果、模型驗證失敗，或框架的空狀態。
    /// </summary>
    public PublicProblem Problem(int status, string code, string? recordedIssue = null) => Public(status, code, recordedIssue ?? Record(status, code));

    private string Record(int status, string code)
    {
        var issue = NewCode();
        var rejection = status < 500;
        Write(logger, rejection ? LogLevel.Information : LogLevel.Error, null, code, issue, rejection);
        return issue;
    }

    /// <summary>以查證代碼與錯誤代碼為範圍寫一筆失敗或拒絕事件，並把目前的 Activity 標為錯誤。</summary>
    private static void Write(ILogger target, LogLevel level, Exception? exception, string code, string issue, bool rejection)
    {
        using var scope = target.BeginScope(new Dictionary<string, object?> { ["IssueCode"] = issue, ["ErrorCode"] = code });
        if (rejection) LogRejection(target, level, exception, code, issue);
        else LogFailure(target, level, exception, code, issue);
        Activity.Current?.SetStatus(ActivityStatusCode.Error, DiagnosticRedactor.Text(code, 80));
    }

    private static PublicProblem Public(int status, string code, string issue)
        => new(status, code, status < 500 ? PublicErrorCatalog.Message(code, status) : Message(issue), issue);

    [LoggerMessage(EventId = DiagnosticEvents.Failure, EventName = "operation.failed", Message = "Operation failed with {ErrorCode}; issue {IssueCode}.")]
    private static partial void LogFailure(ILogger logger, LogLevel level, Exception? exception, string errorCode, string issueCode);

    // 診斷日誌提供者的最低層級通常高於拒絕事件，因此這裡必須略過 IsEnabled 檢查，否則拒絕紀錄會被丟掉。
    [LoggerMessage(EventId = DiagnosticEvents.Rejection, EventName = "http.rejected", Message = "Operation failed with {ErrorCode}; issue {IssueCode}.", SkipEnabledCheck = true)]
    private static partial void LogRejection(ILogger logger, LogLevel level, Exception? exception, string errorCode, string issueCode);

    /// <summary>
    /// SSE 串流已經開始時，把錯誤寫成最後一個 <c>error</c> 事件，然後正常結束串流。
    /// 只給 Problems 的寫入器呼叫；其餘錯誤都經由標準的 IProblemDetailsService 傳遞。
    /// </summary>
    internal static Task WriteEventAsync(HttpContext http, PublicProblem problem)
        => http.Response.WriteAsync("event: error\ndata: " + System.Text.Json.JsonSerializer.Serialize(new { status = problem.Status, code = problem.Code, message = problem.Status >= 500 ? Message(problem.IssueCode) : problem.Title, issueCode = problem.IssueCode }) + "\n\n", http.RequestAborted);

    internal static Task WriteAsync(HttpContext http, PublicProblem problem, IReadOnlyDictionary<string, string[]>? errors = null)
        => Results.Json(new SafeProblemDetails("urn:ai-nexus:problem:" + problem.Code, problem.Title, problem.Status, problem.Code, problem.IssueCode, errors),
            statusCode: problem.Status, contentType: "application/problem+json").ExecuteAsync(http);
}

/// <summary>
/// HTTP 請求的診斷與可觀測性中介軟體：套用 WebSecurity 標頭、建立受信任的 W3C Trace、
/// 捕捉例外，並轉成已脫敏的 RFC 7807 錯誤。
/// </summary>
public sealed partial class DiagnosticRequestMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext http, Issues issues, ILogger<DiagnosticRequestMiddleware> logger)
    {
        WebSecurity.Headers(http);
        var previous = Activity.Current;
        Activity.Current = null;

        using var activity = DiagnosticTrace.Start("http.request", kind: ActivityKind.Server);
        http.Items["Diagnostics.TraceId"] = activity.TraceId.ToHexString();

        var watch = Stopwatch.StartNew();
        var operation = Guid.NewGuid();
        http.TraceIdentifier = Guid.NewGuid().ToString("N");
        activity.SetTag("operation.id", operation.ToString());

        var address = http.Connection.RemoteIpAddress;
        if (address?.IsIPv4MappedToIPv6 == true)
        {
            address = address.MapToIPv4();
        }

        // 連線中繼資料可能已由主機信任的反向代理中介軟體解析過。
        // 這裡不可重新解析客戶端能任意偽造的 X-Forwarded-For 標頭。
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["RequestId"] = http.TraceIdentifier,
            ["OperationId"] = operation,
            ["Method"] = http.Request.Method,
            ["ClientAddress"] = address?.ToString(),
            ["UserAgent"] = DiagnosticRedactor.Text(http.Request.Headers.UserAgent.ToString(), 240),
            ["RequestProtocol"] = http.Request.Protocol,
            ["RequestScheme"] = http.Request.Scheme
        });

        var outcome = "completed";
        try
        {
            // 框架的空狀態錯誤（驗證挑戰、禁止存取、無匹配路由）交給 UseStatusCodePages 產生本體。
            await next(http);
        }
        catch (OperationCanceledException) when (http.RequestAborted.IsCancellationRequested)
        {
            outcome = "cancelled";
        }
        catch (Exception exception)
        {
            outcome = "failed";
            using var failureScope = logger.BeginScope(new Dictionary<string, object?>
            {
                ["Method"] = http.Request.Method,
                ["Route"] = (http.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText,
                ["StatusCode"] = Issues.Classify(exception).Status
            });

            if (!http.Response.HasStarted)
            {
                http.Response.Clear();
                WebSecurity.Headers(http);
                await Problems.WriteAsync(http, exception);
            }
            else if (http.Response.ContentType?.StartsWith("text/event-stream", StringComparison.OrdinalIgnoreCase) == true && !http.RequestAborted.IsCancellationRequested)
            {
                await Issues.WriteEventAsync(http, issues.Problem(exception));
            }
            else
            {
                http.Abort();
            }
        }
        finally
        {
            watch.Stop();
            var duration = Math.Round(watch.Elapsed.TotalMilliseconds, 3);
            var route = (http.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;

            activity.SetTag("http.route", route);
            activity.SetTag("http.request.method", http.Request.Method);
            activity.SetTag("http.response.status_code", http.Response.StatusCode);
            activity.SetTag("client.address", address?.ToString());
            activity.SetTag("network.protocol.version", http.Request.Protocol.Replace("HTTP/", "", StringComparison.OrdinalIgnoreCase));

            if (outcome == "failed" || http.Response.StatusCode >= 500)
            {
                activity.SetStatus(ActivityStatusCode.Error);
            }

            var user = http.RequestServices.GetService<AiNexus.Platform.Security.IRequestUser>()?.ResolvedId;
            using var completionScope = logger.BeginScope(new Dictionary<string, object?>
            {
                ["Method"] = http.Request.Method,
                ["Route"] = route,
                ["StatusCode"] = http.Response.StatusCode,
                ["DurationMs"] = duration,
                ["UserId"] = user,
                ["RequestOutcome"] = outcome,
                ["RequestAborted"] = http.RequestAborted.IsCancellationRequested,
                ["ResponseStarted"] = http.Response.HasStarted
            });

            if (outcome != "completed" || http.Response.StatusCode >= 400 || http.GetEndpoint()?.Metadata.GetMetadata<SuppressSuccessfulRequestLog>() is null)
            {
                LogRequestFinished(logger, http.Response.StatusCode, duration);
            }

            Activity.Current = previous;
        }
    }

    [LoggerMessage(EventId = DiagnosticEvents.Request, EventName = "http.completed", Level = LogLevel.Information, Message = "HTTP request finished with {StatusCode} in {DurationMs:0.###} ms.")]
    private static partial void LogRequestFinished(ILogger logger, int statusCode, double durationMs);
}
