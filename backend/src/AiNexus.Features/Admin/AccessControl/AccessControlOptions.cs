using System.ComponentModel.DataAnnotations;
using AiNexus.Platform.Configuration;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.AccessControl;

public sealed class AccessControlOptions
{
    public const string Section = "AccessControl";

    /// <summary>
    /// 選單外部連結允許的主機：小寫主機名稱，可帶 <c>:port</c>，<c>*.example.com</c> 表示其子網域（不含 example.com 本身）。
    /// 預設空清單＝不允許任何外部連結（失敗即拒絕）。儲存連結與產生 <c>/me</c> 時各檢查一次，移出清單的主機在設定生效（站台重新啟動）後就不再輸出。
    /// </summary>
    [MaxLength(100), HostPatternItems] public string[] NavigationLinkHosts { get; set; } = [];
}

[OptionsValidator]
public sealed partial class AccessControlOptionsValidator : IValidateOptions<AccessControlOptions>;
