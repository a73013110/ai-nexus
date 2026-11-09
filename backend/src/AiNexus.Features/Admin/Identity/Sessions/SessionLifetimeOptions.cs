using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Identity.Sessions;

/// <summary>登入工作階段的三個期限：前兩個到了只鎖定（輸入密碼就回到原畫面），硬上限到了才真正登出。</summary>
public sealed class SessionLifetimeOptions : IValidatableObject
{
    public const string Section = "Identity:Session";

    /// <summary>沒有使用者操作多久後鎖定。背景輪詢不算操作。</summary>
    [Range(1, 480)] public int IdleLockMinutes { get; set; } = 30;

    /// <summary>距上次輸入密碼多久後鎖定，不論期間有沒有操作。</summary>
    [Range(1, 24)] public int ReauthenticationHours { get; set; } = 8;

    /// <summary>距上次輸入密碼多久後 cookie 失效，必須重新登入。</summary>
    [Range(1, 168)] public int HardLimitHours { get; set; } = 24;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // 硬上限先到的話 cookie 直接失效，「鎖定後輸入密碼回到原畫面」永遠不會發生。
        if (HardLimitHours < ReauthenticationHours)
            yield return new("HardLimitHours must not be shorter than ReauthenticationHours.", [nameof(HardLimitHours)]);
    }
}

[OptionsValidator]
public sealed partial class SessionLifetimeOptionsValidator : IValidateOptions<SessionLifetimeOptions>;
