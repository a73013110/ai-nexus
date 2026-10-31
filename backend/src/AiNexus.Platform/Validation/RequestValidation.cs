using System.Reflection;
using System.Text.Json;
using AiNexus.Platform.Errors;
using FluentValidation;
using FluentValidation.Results;

namespace AiNexus.Platform.Validation;

/// <summary>
/// 請求驗證器基底類別。可覆寫 <see cref="ProblemCode"/> 以保留特定功能模組既有的公開錯誤代碼。
/// 驗證器是 singleton（<see cref="RequestValidation.AddRequestValidators"/>），只看 request 本身：建構子只注入 singleton（options、<see cref="TimeProvider"/>），
/// 依賴目前使用者、身分或資料庫的規則放 handler。
/// </summary>
public abstract class RequestValidator<T> : AbstractValidator<T>
{
    public virtual string ProblemCode => RequestValidation.DefaultCode;
}

/// <summary>
/// 在 Handler 執行前，自動驗證端點引數中所有有 <see cref="IValidator{T}"/> 的型別。
/// 套在路由群組上即全面生效，新增端點時不會漏掉驗證；驗證器在建置端點時就解析好，沒有驗證器的端點不掛過濾器，有的端點請求時也不經過 DI。
/// </summary>
public static class RequestValidation
{
    public const string DefaultCode = "validation_failed";

    private static readonly MethodInfo ValidateDefinition = typeof(RequestValidation).GetMethod(nameof(Validate), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>註冊組件內的驗證器。一律 singleton：規則只建一次，過濾器才能在建置端點時解析驗證器。</summary>
    public static IServiceCollection AddRequestValidators(this IServiceCollection services, Assembly assembly)
        => services.AddValidatorsFromAssembly(assembly, ServiceLifetime.Singleton, includeInternalTypes: true);

    public static RouteGroupBuilder WithRequestValidation(this RouteGroupBuilder group) => group.AddEndpointFilterFactory(Create);

    private static EndpointFilterDelegate Create(EndpointFilterFactoryContext context, EndpointFilterDelegate next)
    {
        var parameters = context.MethodInfo.GetParameters();
        // 由後往前包，請求時才會依參數順序驗證，並在第一個失敗的參數回應。
        for (var index = parameters.Length - 1; index >= 0; index--)
        {
            var type = parameters[index].ParameterType;
            // 這幾種型別不能當泛型引數，MakeGenericType 會丟例外。
            if (type.IsByRef || type.IsPointer || type.ContainsGenericParameters) continue;
            if (context.ApplicationServices.GetService(typeof(IValidator<>).MakeGenericType(type)) is { } validator)
                next = (EndpointFilterDelegate)ValidateDefinition.MakeGenericMethod(type).Invoke(null, [index, validator, next])!;
        }
        return next;
    }

    // 泛型版本讓請求時以 GetArgument<T> 取引數（RDG 產生的 context 不必裝箱）並直接呼叫強型別驗證，不經非泛型 context 轉換。
    private static EndpointFilterDelegate Validate<T>(int index, IValidator<T> validator, EndpointFilterDelegate next)
    {
        var code = validator is RequestValidator<T> coded ? coded.ProblemCode : DefaultCode;
        return async invocation =>
        {
            // 選填 body 沒送時是 null；必填 body 缺少時，框架在進過濾器前就已回 400。
            if (invocation.GetArgument<T>(index) is not { } argument) return await next(invocation);
            var result = await validator.ValidateAsync(argument, invocation.HttpContext.RequestAborted);
            return result.IsValid ? await next(invocation) : Error.Invalid(code).ToProblem(Fields(result));
        };
    }

    /// <summary>
    /// 把驗證錯誤轉成「JSON 成員路徑（camelCase）→ 穩定規則代碼」的字典。
    /// 不回傳驗證器的原始訊息，也不回傳使用者提交的值，避免洩漏內容。
    /// </summary>
    private static Dictionary<string, string[]> Fields(ValidationResult result) => result.Errors
        .GroupBy(failure => Path(failure.PropertyName), StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.Select(failure => Rule(failure.ErrorCode)).Distinct(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);

    private static string Path(string property) => string.Join('.', property.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));

    // FluentValidation 內建規則的代碼（如 MaximumLengthValidator）會轉為 snake_case；
    // 自訂規則若需要固定代碼，應改用 WithErrorCode 指定。
    private static string Rule(string code) => code.EndsWith("Validator", StringComparison.Ordinal)
        ? JsonNamingPolicy.SnakeCaseLower.ConvertName(code[..^"Validator".Length])
        : code;
}
