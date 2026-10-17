using System.ComponentModel.DataAnnotations;

namespace AiNexus.Platform.Configuration;

/// <summary>
/// 伺服器受控之 HTTP(S) 端點驗證屬性：必須為絕對 URL，且嚴格禁止包含使用者資訊（user info）、查詢字串（query）或片段標記（fragment），
/// 確保認證金鑰與參數絕不透過組態 URL 傳遞。空值視為合法；若為必填請與 <see cref="RequiredAttribute"/> 搭配使用。
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class HttpEndpointAttribute : ValidationAttribute
{
    /// <summary>除本機迴路（Loopback）主機外，全面強制使用 HTTPS 通訊協定。</summary>
    public bool HttpsOutsideLoopback { get; set; }

    public HttpEndpointAttribute() : base("{0} must be an absolute HTTP(S) URL without user info, query or fragment.") { }

    public override bool IsValid(object? value) => value is null or "" || value is string text && Valid(text, HttpsOutsideLoopback);

    public static bool Valid(string value, bool httpsOutsideLoopback = false) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp && (!httpsOutsideLoopback || uri.IsLoopback))
        && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0;
}
