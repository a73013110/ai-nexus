using System.ComponentModel.DataAnnotations;
using AiNexus.Platform.Configuration;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Platform.Gd;

/// <summary>
/// 公文系統網站的設定，區段為 <c>Gd</c>；資料庫連線另在 <c>Databases:Gd</c>。
/// 網址只在伺服器端使用（附件由後端代為取回），所以換主機、換埠只改這裡，前端與已發出的附件憑證都不必動。
/// </summary>
public sealed class GdOptions
{
    public const string Section = "Gd";

    /// <summary>公文系統網站的根目錄，結尾要有 <c>/</c>（例如 <c>https://gd.example.test/eDoc/</c>）。</summary>
    [Required, HttpEndpoint, RegularExpression(".*/$", ErrorMessage = "{0} must end with '/'.")]
    public string WebBaseUrl { get; set; } = "https://gd.hanglong.com.tw:9923/eDoc/";

    /// <summary>取一個附件的逾時；公文系統卡住時讓預覽早點顯示「無法取得」。</summary>
    [Range(2, 120)] public int FileTimeoutSeconds { get; set; } = 20;

    /// <summary>單一附件的大小上限：後端整個讀進記憶體後才回應，所以要有上限。</summary>
    [Range(1024 * 1024, 100 * 1024 * 1024)] public int MaxFileBytes { get; set; } = 30 * 1024 * 1024;
}

[OptionsValidator]
public sealed partial class GdOptionsValidator : IValidateOptions<GdOptions>;
