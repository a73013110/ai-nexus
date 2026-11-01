namespace AiNexus.Platform.Errors;

/// <summary>
/// 不屬於任何模組的通用錯誤：框架空狀態、管線中介軟體與無法分類的例外都回這幾個代碼，模組要回通用代碼時也用這裡的。
/// 模組不再宣告同代碼的錯誤（<c>ErrorDefinitionTests</c>）；意思更精確時另取專屬代碼。
/// </summary>
public static class HttpErrors
{
    public static readonly Error InvalidRequest = Error.Invalid("invalid_request");
    public static readonly Error AuthenticationRequired = Error.Unauthenticated("authentication_required");
    public static readonly Error AccessDenied = Error.Forbidden("access_denied");
    public static readonly Error CsrfInvalid = Error.Forbidden("csrf_invalid");
    public static readonly Error NotFound = Error.NotFound("not_found");
    public static readonly Error RequestTooLarge = Error.TooLarge("request_too_large");
    public static readonly Error RateLimited = Error.RateLimited("rate_limited");
    public static readonly Error ServiceUnavailable = Error.Unavailable("service_unavailable");
}
