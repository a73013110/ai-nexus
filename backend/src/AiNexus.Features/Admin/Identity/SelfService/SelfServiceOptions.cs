using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Identity.SelfService;

/// <summary>帳號自助維護（設定 → 帳號與權限）：驗證碼、步升驗證與本地密碼新鮮度。</summary>
public sealed class SelfServiceOptions
{
    public const string Section = "Identity:SelfService";

    /// <summary>是否允許使用者自行啟用或變更本地登入（帳號名＋密碼）。關閉時既有的本地登入照常可用，只是不能自行修改。</summary>
    public bool LocalLogin { get; set; } = true;

    /// <summary>步升驗證（重新輸入密碼或驗證碼）後，可進行敏感變更的分鐘數。</summary>
    [Range(1, 60)] public int ReauthenticationMinutes { get; set; } = 10;

    /// <summary>驗證碼有效分鐘數。</summary>
    [Range(5, 60)] public int CodeMinutes { get; set; } = 15;

    /// <summary>同一組驗證碼可輸入錯誤的次數；達到即作廢，需重新索取。</summary>
    [Range(3, 10)] public int CodeAttempts { get; set; } = 5;

    /// <summary>每人每小時最多寄出的驗證碼數（不分用途）。</summary>
    [Range(1, 20)] public int CodesPerHour { get; set; } = 5;

    /// <summary>同一用途重新寄送前要等的秒數。</summary>
    [Range(0, 600)] public int CodeCooldownSeconds { get; set; } = 60;

    /// <summary>
    /// AD 綁定使用者（且仍允許 AD 登入）最近一次 AD 驗證超過這個天數，就不能用本地密碼登入，須先用 AD 登入一次。
    /// 讓 AD 已停用的離職者，自行設定的本地密碼最晚在這個期限後失效。0 表示不限制（預設）。
    /// </summary>
    [Range(0, 365)] public int DirectoryFreshnessDays { get; set; }
}

[OptionsValidator]
public sealed partial class SelfServiceOptionsValidator : IValidateOptions<SelfServiceOptions>;
