using Microsoft.Extensions.Options;

namespace AiNexus.Platform.Configuration;

public static class SettingsRegistration
{
    /// <summary>
    /// Binds a settings section to <typeparamref name="T"/> and validates it with a source-generated
    /// <c>[OptionsValidator]</c> when the host starts. Unknown keys fail binding and invalid values fail validation;
    /// both messages name the key, so a misconfigured machine never starts with silent defaults.
    /// </summary>
    public static OptionsBuilder<T> AddSettings<T, TValidator>(this IServiceCollection services, string section)
        where T : class where TValidator : class, IValidateOptions<T>
    {
        services.AddSingleton<IValidateOptions<T>, TValidator>();
        return services.AddOptions<T>().BindConfiguration(section, binder => binder.ErrorOnUnknownConfiguration = true).ValidateOnStart();
    }
}
