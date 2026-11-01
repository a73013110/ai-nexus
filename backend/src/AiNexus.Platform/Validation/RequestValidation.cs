using System.Collections.Frozen;
using System.Text.Json;
using AiNexus.Platform.Errors;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiNexus.Platform.Validation;

/// <summary>
/// 套用到端點的請求驗證器，只由 <see cref="RequestValidator{T}"/> 實作（建立過濾器的成員是 internal，其他組件無法實作）。
/// 過濾器工廠依 <see cref="RequestType"/> 查表，不必在建置端點時以反射建立泛型型別或方法。
/// </summary>
public interface IRequestValidator
{
    /// <summary>驗證的 request 型別；端點參數是這個型別時才套用。</summary>
    Type RequestType { get; }

    /// <summary>驗證失敗時回應的錯誤；種類必須是 <see cref="ErrorKind.Invalid"/>、代碼要在 <c>PublicErrorCatalog</c>（<c>EndpointConventionTests</c> 檢查）。</summary>
    Error Problem { get; }

    /// <summary>包住 <paramref name="next"/>：驗證第 <paramref name="index"/> 個引數，失敗時回應 problem、不呼叫 handler。</summary>
    internal EndpointFilterDelegate Wrap(int index, EndpointFilterDelegate next);
}

/// <summary>
/// 請求驗證器基底類別，只有它會套用到端點；單純的 <see cref="AbstractValidator{T}"/> 只當子驗證器（<c>Include</c>、<c>SetValidator</c>）。
/// 功能有自己的提示時覆寫 <see cref="Problem"/>，回傳模組 <c>&lt;Module&gt;Errors</c> 裡的錯誤；預設是 <see cref="RequestValidation.Failed"/>。
/// 驗證器由組合產生器以 <see cref="IRequestValidator"/> 註冊為 singleton（<see cref="Modules.FeatureCompositionAttribute"/>），只看 request 本身：
/// 建構子只注入 singleton（options、<see cref="TimeProvider"/>），依賴目前使用者、身分或資料庫的規則放 handler。
/// </summary>
public abstract class RequestValidator<T> : AbstractValidator<T>, IRequestValidator
{
    public virtual Error Problem => RequestValidation.Failed;

    Type IRequestValidator.RequestType => typeof(T);

    // 在泛型型別裡建立過濾器，請求時以 GetArgument<T> 取引數（RDG 產生的 context 不必裝箱）並直接呼叫強型別驗證。
    EndpointFilterDelegate IRequestValidator.Wrap(int index, EndpointFilterDelegate next)
    {
        var problem = Problem;
        return async invocation =>
        {
            // 選填 body 沒送時是 null；必填 body 缺少時，框架在進過濾器前就已回 400。
            if (invocation.GetArgument<T>(index) is not { } argument) return await next(invocation);
            var result = await ValidateAsync(argument, invocation.HttpContext.RequestAborted);
            return result.IsValid ? await next(invocation) : problem.ToProblem(RequestValidation.Fields(result));
        };
    }
}

/// <summary>依 request 型別查詢 <see cref="IRequestValidator"/>。同一型別只能有一個驗證器，否則建立時（啟動檢查）就失敗。</summary>
public sealed class RequestValidators
{
    private readonly FrozenDictionary<Type, IRequestValidator> byType;

    internal RequestValidators(IEnumerable<IRequestValidator> validators)
    {
        var all = validators.ToList();
        // 只取一個會讓另一個的規則悄悄失效；組合產生器在編譯期已擋下（AINX1005），這裡攔住模組自行註冊的。
        var duplicates = all.GroupBy(x => x.RequestType).Where(g => g.Count() > 1)
            .Select(g => $"{g.Key.FullName}: {string.Join(", ", g.Select(x => x.GetType().FullName).Order(StringComparer.Ordinal))}").ToList();
        if (duplicates.Count > 0)
            throw new InvalidOperationException("Each request type can have only one RequestValidator<T>:\n" + string.Join('\n', duplicates));
        byType = all.ToFrozenDictionary(x => x.RequestType);
    }

    internal IRequestValidator? Find(Type requestType) => byType.GetValueOrDefault(requestType);
}

/// <summary>
/// 在 Handler 執行前，驗證端點引數中所有有 <see cref="RequestValidator{T}"/> 的型別。
/// 套在路由群組上即全面生效，新增端點時不會漏掉驗證；驗證器在建置端點時就查好，沒有驗證器的端點不掛過濾器，有的端點請求時也不經過 DI。
/// </summary>
public static class RequestValidation
{
    /// <summary>沒有功能專屬提示時的驗證錯誤：「請檢查標示的欄位」。</summary>
    public static readonly Error Failed = Error.Invalid("validation_failed");

    /// <summary>註冊驗證器的查詢表；驗證器本身由組合產生器註冊。</summary>
    public static IServiceCollection AddRequestValidation(this IServiceCollection services)
    {
        services.TryAddSingleton(provider => new RequestValidators(provider.GetServices<IRequestValidator>()));
        return services;
    }

    public static RouteGroupBuilder WithRequestValidation(this RouteGroupBuilder group) => group.AddEndpointFilterFactory(Create);

    private static EndpointFilterDelegate Create(EndpointFilterFactoryContext context, EndpointFilterDelegate next)
    {
        var validators = context.ApplicationServices.GetRequiredService<RequestValidators>();
        var parameters = context.MethodInfo.GetParameters();
        // 由後往前包，請求時才會依參數順序驗證，並在第一個失敗的參數回應。
        for (var index = parameters.Length - 1; index >= 0; index--)
            if (validators.Find(parameters[index].ParameterType) is { } validator) next = validator.Wrap(index, next);
        return next;
    }

    /// <summary>
    /// 把驗證錯誤轉成「JSON 成員路徑（camelCase）→ 穩定規則代碼」的字典。
    /// 不回傳驗證器的原始訊息，也不回傳使用者提交的值，避免洩漏內容。
    /// </summary>
    internal static Dictionary<string, string[]> Fields(ValidationResult result) => result.Errors
        .GroupBy(failure => Path(failure.PropertyName), StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.Select(failure => Rule(failure.ErrorCode)).Distinct(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);

    private static string Path(string property) => string.Join('.', property.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));

    // FluentValidation 內建規則的代碼（如 MaximumLengthValidator）會轉為 snake_case；
    // 自訂規則若需要固定代碼，應改用 WithErrorCode 指定。
    private static string Rule(string code) => code.EndsWith("Validator", StringComparison.Ordinal)
        ? JsonNamingPolicy.SnakeCaseLower.ConvertName(code[..^"Validator".Length])
        : code;
}
