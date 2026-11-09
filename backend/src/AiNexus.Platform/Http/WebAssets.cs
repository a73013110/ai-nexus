using System.IO.Compression;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.ResponseCompression;

namespace AiNexus.Platform.Http;

/// <summary>前端靜態檔的快取與壓縮。檔名帶雜湊的內容不會變，可以長期快取；其餘（尤其 index.html）每次都要回來確認，新版才會立刻生效。</summary>
public static partial class WebAssets
{
    private const string Immutable = "public, max-age=31536000, immutable";
    private const string Revalidate = "no-cache";

    /// <summary>靜態檔與 SPA 外殼共用；外殼經由 fallback 送出，也要走這裡才不會被瀏覽器留住舊版。</summary>
    public static readonly StaticFileOptions Files = new()
    {
        OnPrepareResponse = context => context.Context.Response.Headers.CacheControl =
            HashedFile().IsMatch(context.Context.Request.Path.Value ?? "") ? Immutable : Revalidate,
    };

    public static IServiceCollection AddWebAssetCompression(this IServiceCollection services)
    {
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
            options.MimeTypes = ["text/html", "text/css", "text/javascript", "application/javascript", "image/svg+xml"];
        });
        // 帶雜湊的檔案瀏覽器只抓一次；Optimal 的 CPU 成本只付在第一次，傳輸量卻省在每位使用者身上。
        services.Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Optimal);
        services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Optimal);
        return services;
    }

    /// <summary>API 不進壓縮：SSE 要逐筆送出，回應裡的個人資料與防偽權杖也不該和壓縮長度一起暴露（BREACH）。</summary>
    public static WebApplication UseWebAssets(this WebApplication app)
    {
        app.UseWhen(http => !http.Request.Path.StartsWithSegments("/api"), branch => branch.UseResponseCompression());
        app.UseDefaultFiles();
        app.UseStaticFiles(Files);
        return app;
    }

    // 只認建置器產生的根目錄檔名：vendor/ 與 boot.js 這類固定檔名就算剛好帶連字號，內容也會隨版本改變。
    [GeneratedRegex(@"^/(?:chunk|main|polyfills|styles)-[A-Za-z0-9_-]{8}\.(?:js|css)$", RegexOptions.CultureInvariant)]
    private static partial Regex HashedFile();
}
