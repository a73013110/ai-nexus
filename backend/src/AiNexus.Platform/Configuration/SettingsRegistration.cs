using Microsoft.Extensions.Options;

namespace AiNexus.Platform.Configuration;

/// <summary>設定註冊擴充方法。提供強型別組態區段繫結與啟動時來源產生驗證器機制。</summary>
public static class SettingsRegistration
{
    /// <summary>
    /// 將組態區段繫結至設定型別 <typeparamref name="T"/>，並在主機啟動時透過原始碼產生器產生的 <c>[OptionsValidator]</c> 立即進行嚴格驗證。
    /// 未知設定鍵將導致繫結失敗，不合規數值將導致驗證中斷，兩者皆會明確指出錯誤設定鍵名，確保配置錯誤的伺服器絕不會以隱藏預設值默默啟動。
    /// </summary>
    public static OptionsBuilder<T> AddSettings<T, TValidator>(this IServiceCollection services, string section)
        where T : class where TValidator : class, IValidateOptions<T>
    {
        services.AddSingleton<IValidateOptions<T>, TValidator>();
        return services.AddOptions<T>().BindConfiguration(section, binder => binder.ErrorOnUnknownConfiguration = true).ValidateOnStart();
    }
}
