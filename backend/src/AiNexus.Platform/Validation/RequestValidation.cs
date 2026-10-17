using System.Text.Json;
using AiNexus.Platform.Errors;
using FluentValidation;
using FluentValidation.Results;

namespace AiNexus.Platform.Validation;

/// <summary>請求驗證器基底類別。可覆寫 <see cref="ProblemCode"/> 以保留特定功能模組既有的公開錯誤代碼。</summary>
public abstract class RequestValidator<T> : AbstractValidator<T>, IRequestValidator
{
    public virtual string ProblemCode => RequestValidation.DefaultCode;
}

public interface IRequestValidator
{
    string ProblemCode { get; }
}

/// <summary>
/// 在 Handler 執行前，自動驗證端點引數中所有有 <see cref="IValidator{T}"/> 的型別。
/// 套在路由群組上即全面生效，新增端點時不會漏掉驗證；沒有驗證器的端點不會增加過濾器開銷。
/// </summary>
public static class RequestValidation
{
    public const string DefaultCode = "validation_failed";

    public static RouteGroupBuilder WithRequestValidation(this RouteGroupBuilder group) => group.AddEndpointFilterFactory(Create);

    private static EndpointFilterDelegate Create(EndpointFilterFactoryContext context, EndpointFilterDelegate next)
    {
        var registered = context.ApplicationServices.GetRequiredService<IServiceProviderIsService>();
        var targets = context.MethodInfo.GetParameters()
            .Select((parameter, index) => (Index: index, Type: parameter.ParameterType))
            .Where(x => x.Type is { IsByRef: false, IsPointer: false, ContainsGenericParameters: false })
            .Select(x => (x.Index, Validator: typeof(IValidator<>).MakeGenericType(x.Type)))
            .Where(x => registered.IsService(x.Validator))
            .ToArray();
        if (targets.Length == 0) return next;
        return async invocation =>
        {
            foreach (var (index, validatorType) in targets)
            {
                if (invocation.Arguments[index] is not { } argument) continue;
                var validator = (IValidator)invocation.HttpContext.RequestServices.GetRequiredService(validatorType);
                var result = await validator.ValidateAsync(new ValidationContext<object>(argument), invocation.HttpContext.RequestAborted);
                if (!result.IsValid)
                    return Error.Invalid(validator is IRequestValidator coded ? coded.ProblemCode : DefaultCode).ToProblem(Fields(result));
            }
            return await next(invocation);
        };
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
