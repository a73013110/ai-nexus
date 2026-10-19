using Microsoft.AspNetCore.Http.Features;

namespace AiNexus.Host;

/// <summary>
/// 伺服器全域請求與上傳大小配額上限。
/// 各端點會宣告更嚴格的限制（見 RequestBodyLimits）；這裡只設最寬鬆的全域上限，
/// 避免 Kestrel 與 IIS 先接受超過應用程式可承受的承載量。
/// </summary>
internal static class ServerLimits
{
    // Kestrel 與 IIS 必須同一個值：本機與 IIS 部署才會在同一個大小拒絕請求。
    private const long MaxRequestBodyBytes = 10 * 1024 * 1024;

    // 比請求上限小，multipart 邊界與其他欄位才有餘裕，不會在檔案剛好接近上限時被請求上限先擋下。
    private const long MaxMultipartBodyBytes = 9 * 1024 * 1024;

    public static void ConfigureServerLimits(this WebApplicationBuilder builder)
    {
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.AddServerHeader = false;
            options.Limits.MaxRequestBodySize = MaxRequestBodyBytes;
        });

        builder.Services.Configure<IISServerOptions>(options => options.MaxRequestBodySize = MaxRequestBodyBytes);
        builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = MaxMultipartBodyBytes);
    }
}
