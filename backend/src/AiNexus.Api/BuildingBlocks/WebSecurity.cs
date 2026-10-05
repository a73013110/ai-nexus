using System.Net;

namespace AiNexus.BuildingBlocks;

/// <summary>One policy for sessions, API caching and browser/file isolation.</summary>
public static class WebSecurity
{
    public static bool AllowsLocalHttp(IHostEnvironment environment, IConfiguration configuration) =>
        environment.IsDevelopment() && configuration.GetValue<bool>("Security:AllowInsecureLocalhost");

    public static CookieSecurePolicy CookiePolicy(IHostEnvironment environment, IConfiguration configuration) =>
        environment.IsEnvironment("Testing") || AllowsLocalHttp(environment, configuration)
            ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;

    public static void NoStore(HttpResponse response)
    {
        // Matches antiforgery's policy before tokens are generated, including anonymous login.
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
        headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self'; connect-src 'self'; font-src 'self'; worker-src 'self'; object-src 'none'; base-uri 'self'; frame-ancestors 'none'; form-action 'self'";
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

    public static IResult File(HttpContext http, byte[] bytes, string contentType, string name, bool download)
    {
        NoStore(http.Response);
        // Uploaded data never inherits the application's scripting privileges when opened directly.
        http.Response.Headers["Content-Security-Policy"] = "default-src 'none'; sandbox; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";
        var inline = !download && (contentType is "application/pdf" or "image/png" or "image/jpeg" or "image/webp");
        return Results.File(bytes, contentType, fileDownloadName: inline ? null : name);
    }
}
