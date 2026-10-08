using AiNexus.Platform.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AiNexus.Platform.Errors;

/// <summary>
/// The single place that turns failures into the public <see cref="SafeProblemDetails"/> body. Every producer
/// (expected <see cref="Error"/> results, validation, thrown exceptions, empty framework status codes) goes through
/// <see cref="IProblemDetailsService"/>, so the format, logging and issue codes cannot diverge.
/// </summary>
public static class Problems
{
    internal const string CodeKey = "code", ErrorsKey = "errors";

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

    public static IResult ToProblem(this Error error, IReadOnlyDictionary<string, string[]>? errors = null)
        => TypedResults.Problem(Details(Status(error.Kind), error.Code, errors));

    public static IResult ToHttpResult<T>(this Result<T> result) => result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();

    public static IResult ToHttpResult(this Result result) => result.IsSuccess ? TypedResults.NoContent() : result.Error.ToProblem();

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
        var problem = issues.Problem(context.Exception ?? new ApiException(status,
            details.Extensions.TryGetValue(Problems.CodeKey, out var code) && code is string value ? value : Problems.DefaultCode(status), ""));
        var errors = details.Extensions.TryGetValue(Problems.ErrorsKey, out var fields) ? fields as IReadOnlyDictionary<string, string[]> : null;
        return new(Issues.WriteAsync(http, problem, errors));
    }
}
