namespace AiNexus.Platform.Errors;

/// <summary>錯誤分類列舉，獨立於傳輸層通訊協定。<see cref="Problems"/> 是全系統唯一將其對應至 HTTP 狀態碼之處。</summary>
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

/// <summary>預期內的業務失敗。<see cref="Code"/> 為公開穩定的識別碼，列於 <c>PublicErrorCatalog</c>。</summary>
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
