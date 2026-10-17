using AiNexus.Platform.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace AiNexus.Platform.Errors;

/// <summary>
/// 全系統唯一將執行失敗轉換為公開 <see cref="SafeProblemDetails"/> 回應結構之處。
/// 任何失敗來源（預期內的 <see cref="Error"/> 結果、模型驗證錯誤、拋出之例外、框架預設空狀態碼）皆經由
/// <see cref="IProblemDetailsService"/> 處理，確保錯誤格式、日誌記錄與查證代碼（issue code）完全一致。
/// </summary>
public static class Problems
{
    internal const string CodeKey = "code", ErrorsKey = "errors", IssueKey = "issueCode";

    public static int Status(ErrorKind kind) => kind switch
    {
        ErrorKind.Invalid => StatusCodes.Status400BadRequest,
        ErrorKind.Unauthenticated => StatusCodes.Status401Unauthorized,
        ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        ErrorKind.NotFound => StatusCodes.Status404NotFound,
        ErrorKind.Conflict => StatusCodes.Status409Conflict,
        ErrorKind.TooLarge => StatusCodes.Status413PayloadTooLarge,
        ErrorKind.Unprocessable => StatusCodes.Status422UnprocessableEntity,
        ErrorKind.RateLimited => StatusCodes.Status429TooManyRequests,
        ErrorKind.Upstream => StatusCodes.Status502BadGateway,
        ErrorKind.Unavailable => StatusCodes.Status503ServiceUnavailable,
        ErrorKind.Timeout => StatusCodes.Status504GatewayTimeout,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>針對框架未附帶回應本體的狀態碼（如驗證挑戰、存取拒絕、未配對路由）提供預設公開代碼。</summary>
    public static string DefaultCode(int status) => status switch
    {
        401 => "authentication_required",
        403 => "access_denied",
        404 => "not_found",
        429 => "rate_limited",
        _ => "invalid_request",
    };

    public static ProblemHttpResult ToProblem(this Error error, IReadOnlyDictionary<string, string[]>? errors = null)
        => TypedResults.Problem(Details(Status(error.Kind), error.Code, errors));

    /// <summary>
    /// 適用於已由 <see cref="Issues.Report(Error)"/> 記錄過的失敗（例如同時要寫入稽核日誌）。
    /// 回應會沿用既有的查證代碼，不另外產生新的。
    /// </summary>
    public static ProblemHttpResult ToProblem(this Error error, string issueCode)
    {
        var details = Details(Status(error.Kind), error.Code, null);
        details.Extensions[IssueKey] = issueCode;
        return TypedResults.Problem(details);
    }

    public static Results<Ok<T>, ProblemHttpResult> ToHttpResult<T>(this Result<T> result)
        => result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();

    public static Results<NoContent, ProblemHttpResult> ToHttpResult(this Result result)
        => result.IsSuccess ? TypedResults.NoContent() : result.Error.ToProblem();

    /// <summary>適用於成功時不回傳 JSON 本體的端點，例如檔案下載、重新導向或 <c>201 Created</c>。</summary>
    public static Results<TSuccess, ProblemHttpResult> ToHttpResult<T, TSuccess>(this Result<T> result, Func<T, TSuccess> success)
        where TSuccess : IResult => result.IsSuccess ? success(result.Value) : result.Error.ToProblem();

    public static async Task<Results<Ok<T>, ProblemHttpResult>> ToHttpResultAsync<T>(this Task<Result<T>> result)
        => (await result).ToHttpResult();

    public static async Task<Results<NoContent, ProblemHttpResult>> ToHttpResultAsync(this Task<Result> result)
        => (await result).ToHttpResult();

    public static async Task<Results<TSuccess, ProblemHttpResult>> ToHttpResultAsync<T, TSuccess>(this Task<Result<T>> result, Func<T, TSuccess> success)
        where TSuccess : IResult => (await result).ToHttpResult(success);

    /// <summary>
    /// 適用於 Server-Sent Events (SSE) 串流：
    /// 若串流尚未開始，回傳標準 ProblemDetails；若已開始傳輸，則在串流結尾送出 <c>error</c> 事件，內容與公開 Problem 資料相同。
    /// </summary>
    public static async Task<Results<EmptyHttpResult, ProblemHttpResult>> ToStreamResultAsync(this Task<Result> result, HttpContext http)
    {
        var outcome = await result;
        if (outcome.IsSuccess) return TypedResults.Empty;
        if (!http.Response.HasStarted) return outcome.Error.ToProblem();
        if (!http.RequestAborted.IsCancellationRequested)
            await Issues.WriteEventAsync(http, http.RequestServices.GetRequiredService<Issues>().Problem(Status(outcome.Error.Kind), outcome.Error.Code));
        return TypedResults.Empty;
    }

    /// <summary>供端點管線之外的中介軟體（Middleware）直接寫出錯誤回應。</summary>
    public static Task WriteAsync(HttpContext http, int status, string code)
        => http.RequestServices.GetRequiredService<IProblemDetailsService>()
            .WriteAsync(new() { HttpContext = http, ProblemDetails = Details(status, code, null) }).AsTask();

    /// <summary>把請求管線中拋出的例外轉成錯誤回應並寫出。</summary>
    public static Task WriteAsync(HttpContext http, Exception exception)
        => http.RequestServices.GetRequiredService<IProblemDetailsService>()
            .WriteAsync(new() { HttpContext = http, Exception = exception, ProblemDetails = new() }).AsTask();

    public static IServiceCollection AddNexusProblemDetails(this IServiceCollection services)
    {
        // 以 NexusProblemWriter 取代框架預設的寫入器。框架預設會拒絕非 application/json 的 Accept 標頭，
        // 而且可能把內部追蹤識別碼寫進公開回應。
        services.AddSingleton<IProblemDetailsWriter, NexusProblemWriter>();
        return services.AddProblemDetails();
    }

    private static ProblemDetails Details(int status, string code, IReadOnlyDictionary<string, string[]>? errors)
    {
        var details = new ProblemDetails { Status = status, Extensions = { [CodeKey] = code } };
        if (errors is not null) details.Extensions[ErrorsKey] = errors;
        return details;
    }
}

internal sealed class NexusProblemWriter(Issues issues) : IProblemDetailsWriter
{
    public bool CanWrite(ProblemDetailsContext context) => true;

    public ValueTask WriteAsync(ProblemDetailsContext context)
    {
        var http = context.HttpContext;
        var details = context.ProblemDetails;
        var status = details.Status ?? http.Response.StatusCode;
        var code = details.Extensions.TryGetValue(Problems.CodeKey, out var named) && named is string value ? value : Problems.DefaultCode(status);
        var problem = context.Exception is { } exception ? issues.Problem(exception)
            : issues.Problem(status, code, details.Extensions.TryGetValue(Problems.IssueKey, out var recorded) ? recorded as string : null);
        var errors = details.Extensions.TryGetValue(Problems.ErrorsKey, out var fields) ? fields as IReadOnlyDictionary<string, string[]> : null;
        return new(Issues.WriteAsync(http, problem, errors));
    }
}
