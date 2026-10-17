namespace AiNexus.Host;

/// <summary>
/// 伺服器全域請求與上傳大小配額上限。
/// 各個別端點均會宣告更小、更嚴格之限制（請參閱 RequestBodyLimits）；此處僅設定最寬鬆之上限防線，
/// 確保 Kestrel 與 IIS 絕不會快取超過應用程式所能接受之最大承載量。
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
