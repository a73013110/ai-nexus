using System.ComponentModel.DataAnnotations;

namespace AiNexus.Platform.Configuration;

/// <summary>
/// 伺服器端受控的 HTTP(S) 端點驗證屬性：必須是絕對 URL，且不得含使用者資訊、查詢字串或片段。
/// 這樣認證金鑰與參數就不會經由組態 URL 傳遞。空值視為合法；若必填，請搭配 <see cref="RequiredAttribute"/>。
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class HttpEndpointAttribute : ValidationAttribute
{
    /// <summary>除本機迴路（Loopback）外，一律強制使用 HTTPS。</summary>
    public bool HttpsOutsideLoopback { get; set; }

    public HttpEndpointAttribute() : base("{0} must be an absolute HTTP(S) URL without user info, query or fragment.") { }

    public override bool IsValid(object? value) => value is null or "" || value is string text && Valid(text, HttpsOutsideLoopback);

    public static bool Valid(string value, bool httpsOutsideLoopback = false) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp && (!httpsOutsideLoopback || uri.IsLoopback))
        && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0;
}
