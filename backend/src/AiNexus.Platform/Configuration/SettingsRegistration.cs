using Microsoft.Extensions.Options;

namespace AiNexus.Platform.Configuration;

/// <summary>設定註冊的擴充方法：提供強型別區段繫結，並在啟動時驗證。</summary>
public static class SettingsRegistration
{
    /// <summary>
    /// 把組態區段綁定到 <typeparamref name="T"/>，並在啟動時由原始碼產生的 <c>[OptionsValidator]</c> 嚴格驗證。
    /// 未知的設定鍵會讓綁定失敗，不合規的值會讓驗證失敗；兩者都會指出錯誤的鍵名。
    /// 這樣設定有誤的伺服器就不會悄悄用預設值啟動。
    /// </summary>
    public static OptionsBuilder<T> AddSettings<T, TValidator>(this IServiceCollection services, string section)
        where T : class where TValidator : class, IValidateOptions<T>
    {
        services.AddSingleton<IValidateOptions<T>, TValidator>();
        return services.AddOptions<T>().BindConfiguration(section, binder => binder.ErrorOnUnknownConfiguration = true).ValidateOnStart();
    }
}
