using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Platform.Google;

/// <summary>
/// Google API 的服務帳戶設定，區段為 <c>Google</c>；所有呼叫 Google API 的模組共用同一個服務帳戶（Gemini 用的 API key 另在 <c>Inference</c>）。
/// 外部設定不會自動重新載入，改了要回收應用程式集區。
/// </summary>
public sealed class GoogleOptions
{
    public const string Section = "Google";

    /// <summary>
    /// 服務帳戶金鑰檔（Google Cloud 主控台下載的 JSON）的路徑，相對路徑以 app 的 content root 為準；空字串表示未設定，用到 Google API 的功能回報未設定。
    /// 金鑰檔等同密碼：放在 app 與 Git 之外，只開放給 IIS 應用程式集區身分讀取。
    /// </summary>
    [MaxLength(500)] public string ServiceAccountKeyFile { get; set; } = "";

    /// <summary>
    /// 全網域委派時代為操作的 Google Workspace 帳號（JWT 的 <c>sub</c>）；空字串表示以服務帳戶本身操作，日曆要分享給服務帳戶的信箱。
    /// </summary>
    [MaxLength(320)] public string DelegatedUser { get; set; } = "";

    /// <summary>一次 Google API 呼叫（含換發權杖）的逾時。</summary>
    [Range(5, 120)] public int TimeoutSeconds { get; set; } = 30;
}

[OptionsValidator]
public sealed partial class GoogleOptionsValidator : IValidateOptions<GoogleOptions>;
