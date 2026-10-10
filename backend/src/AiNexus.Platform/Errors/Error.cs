namespace AiNexus.Platform.Errors;

/// <summary>What went wrong, independent of transport. <see cref="Problems"/> owns the only mapping to HTTP status codes.</summary>
public enum ErrorKind
{
    Invalid,
    Unauthenticated,
    Forbidden,
    NotFound,
    Conflict,
    TooLarge,
    Unprocessable,
    RateLimited,
    Upstream,
    Unavailable,
    Timeout,
}

/// <summary>An expected failure. <see cref="Code"/> is a stable public identifier listed in <c>PublicErrorCatalog</c>.</summary>
public sealed record Error(ErrorKind Kind, string Code)
{
    public static Error Invalid(string code) => new(ErrorKind.Invalid, code);
    public static Error Unauthenticated(string code) => new(ErrorKind.Unauthenticated, code);
    public static Error Forbidden(string code) => new(ErrorKind.Forbidden, code);
    public static Error NotFound(string code) => new(ErrorKind.NotFound, code);
    public static Error Conflict(string code) => new(ErrorKind.Conflict, code);
    public static Error TooLarge(string code) => new(ErrorKind.TooLarge, code);
    public static Error Unprocessable(string code) => new(ErrorKind.Unprocessable, code);
    public static Error RateLimited(string code) => new(ErrorKind.RateLimited, code);
    public static Error Upstream(string code) => new(ErrorKind.Upstream, code);
    public static Error Unavailable(string code) => new(ErrorKind.Unavailable, code);
    public static Error Timeout(string code) => new(ErrorKind.Timeout, code);
}
