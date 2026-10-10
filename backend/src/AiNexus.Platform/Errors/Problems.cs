using AiNexus.Platform.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace AiNexus.Platform.Errors;

/// <summary>
/// The single place that turns failures into the public <see cref="SafeProblemDetails"/> body. Every producer
/// (expected <see cref="Error"/> results, validation, thrown exceptions, empty framework status codes) goes through
/// <see cref="IProblemDetailsService"/>, so the format, logging and issue codes cannot diverge.
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

    /// <summary>Public code for a framework status code that carried no body (challenge, forbid, unmatched route).</summary>
    public static string DefaultCode(int status) => status switch
    {
        401 => "authentication_required", 403 => "access_denied", 404 => "not_found", 429 => "rate_limited", _ => "invalid_request",
    };

    public static ProblemHttpResult ToProblem(this Error error, IReadOnlyDictionary<string, string[]>? errors = null)
        => TypedResults.Problem(Details(Status(error.Kind), error.Code, errors));

    /// <summary>For a failure already recorded with <see cref="Issues.Report(Error)"/>, for example to audit it: the response reuses that issue code.</summary>
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

    /// <summary>For a success that is not <c>200 OK</c> with the value as JSON (a file, a redirect, <c>201 Created</c>).</summary>
    public static Results<TSuccess, ProblemHttpResult> ToHttpResult<T, TSuccess>(this Result<T> result, Func<T, TSuccess> success)
        where TSuccess : IResult => result.IsSuccess ? success(result.Value) : result.Error.ToProblem();

    public static async Task<Results<Ok<T>, ProblemHttpResult>> ToHttpResultAsync<T>(this Task<Result<T>> result)
        => (await result).ToHttpResult();

    public static async Task<Results<NoContent, ProblemHttpResult>> ToHttpResultAsync(this Task<Result> result)
        => (await result).ToHttpResult();

    public static async Task<Results<TSuccess, ProblemHttpResult>> ToHttpResultAsync<T, TSuccess>(this Task<Result<T>> result, Func<T, TSuccess> success)
        where TSuccess : IResult => (await result).ToHttpResult(success);

    /// <summary>
    /// For a server-sent event stream: a failure before the stream started is a problem response; after it started, the
    /// stream ends with an <c>error</c> event carrying the same public problem.
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

    /// <summary>Writes a problem from middleware that runs outside endpoint execution.</summary>
    public static Task WriteAsync(HttpContext http, int status, string code)
        => http.RequestServices.GetRequiredService<IProblemDetailsService>()
            .WriteAsync(new() { HttpContext = http, ProblemDetails = Details(status, code, null) }).AsTask();

    /// <summary>Writes the problem for an exception thrown by the request pipeline.</summary>
    public static Task WriteAsync(HttpContext http, Exception exception)
        => http.RequestServices.GetRequiredService<IProblemDetailsService>()
            .WriteAsync(new() { HttpContext = http, Exception = exception, ProblemDetails = new() }).AsTask();

    public static IServiceCollection AddNexusProblemDetails(this IServiceCollection services)
    {
        // Registered first so it is chosen over the framework writer, which declines for non-JSON Accept headers and
        // would add trace identifiers to the public body.
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
