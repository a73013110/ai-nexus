namespace AiNexus.Host;

/// <summary>
/// 伺服器全域請求與上傳大小配額上限。
/// 各端點會宣告更嚴格的限制（見 RequestBodyLimits）；這裡只設最寬鬆的全域上限，
/// 避免 Kestrel 與 IIS 先接受超過應用程式可承受的承載量。
/// </summary>
internal static class ServerLimits
{
    public static void ConfigureServerLimits(this WebApplicationBuilder builder)
    {
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.AddServerHeader = false;
            options.Limits.MaxRequestBodySize = 10 * 1024 * 1024;
        });

        builder.Services.Configure<IISServerOptions>(options =>
            options.MaxRequestBodySize = 10 * 1024 * 1024);

        builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
            options.MultipartBodyLengthLimit = 9 * 1024 * 1024);
    }
}
