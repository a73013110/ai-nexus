using System.Net;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AiNexus.Platform.Security;

/// <summary>統籌會話（Session）、API 快取控制與瀏覽器/檔案隔離之全域安全性原則。</summary>
public static class WebSecurity
{
    public static bool AllowsLocalHttp(IHostEnvironment environment, IConfiguration configuration) =>
        environment.IsDevelopment() && configuration.GetValue<bool>("Security:AllowInsecureLocalhost");

    public static CookieSecurePolicy CookiePolicy(IHostEnvironment environment, IConfiguration configuration) =>
        environment.IsEnvironment("Testing") || AllowsLocalHttp(environment, configuration)
            ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;

    public static void NoStore(HttpResponse response)
    {
        // 含個人資料的回應不可被瀏覽器或中間代理留存；匿名情境（尚未產生防偽權杖）也一併禁止快取。
        response.Headers.CacheControl = "no-cache, no-store";
        response.Headers.Pragma = "no-cache";
        response.Headers.Expires = "0";
    }

    public static void Headers(HttpContext http)
    {
        var headers = http.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' blob:; connect-src 'self'; font-src 'self'; frame-src 'self'; worker-src 'self'; object-src 'none'; base-uri 'self'; frame-ancestors 'none'; form-action 'self'";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        headers["Cross-Origin-Resource-Policy"] = "same-origin";
        if (http.Request.Path.StartsWithSegments("/api"))
        {
            NoStore(http.Response);
            http.Response.OnStarting(() => { NoStore(http.Response); return Task.CompletedTask; });
        }
    }

    public static bool IsLoopback(HttpRequest request, IPAddress? address) =>
        address is not null && IPAddress.IsLoopback(address) &&
        request.Host.Host is "localhost" or "127.0.0.1" or "::1" or "[::1]";

    public static FileStreamHttpResult File(HttpContext http, byte[] bytes, string contentType, string name, bool download)
        => File(http, new MemoryStream(bytes, writable: false), contentType, name, download);

    public static FileStreamHttpResult File(HttpContext http, Stream stream, string contentType, string name, bool download)
    {
        NoStore(http.Response);
        // 使用者上傳之檔案在被直接開啟時，絕不繼承應用程式之指令碼執行權限（以 CSP 完全限制）。
        http.Response.Headers["Content-Security-Policy"] = "default-src 'none'; sandbox; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";
        var inline = !download && (contentType is "application/pdf" or "image/png" or "image/jpeg" or "image/webp");
        return TypedResults.File(stream, contentType, fileDownloadName: inline ? null : name);
    }

    public static bool IsHtml(string contentType, string name) =>
        contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith(".html", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".htm", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 以完全隔離的沙箱提供上傳的 HTML 內容，使應用程式內建 iframe 能夠安全渲染。
    /// CSP 沙箱中排除 allow-same-origin，確保即使直接開啟亦具備不透明來源（opaque origin），且無法載入任何外部資源或對外發送請求。
    /// </summary>
    public static FileStreamHttpResult SandboxedHtml(HttpContext http, Stream stream)
    {
        NoStore(http.Response);
        var headers = http.Response.Headers;
        headers["Content-Security-Policy"] = "sandbox allow-scripts; default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; img-src data: blob:; font-src data:; media-src data: blob:; connect-src 'none'; form-action 'none'; base-uri 'none'; frame-ancestors 'self'";
        headers["X-Frame-Options"] = "SAMEORIGIN";
        return TypedResults.File(stream, "text/html; charset=utf-8");
    }
}
