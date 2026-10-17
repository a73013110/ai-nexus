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
/// 於 Handler 執行前自動驗證所有具備 <see cref="IValidator{T}"/> 的端點引數。
/// 一次套用於路由群組即可全面生效，避免新增端點時遺漏驗證；無須驗證參數的端點不會產生額外過濾器開銷。
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
            foreach (var (index, type) in targets)
            {
                if (invocation.Arguments[index] is not { } argument) continue;
                var validator = (IValidator)invocation.HttpContext.RequestServices.GetRequiredService(type);
                var result = await validator.ValidateAsync(new ValidationContext<object>(argument), invocation.HttpContext.RequestAborted);
                if (!result.IsValid)
                    return Error.Invalid(validator is IRequestValidator coded ? coded.ProblemCode : DefaultCode).ToProblem(Fields(result));
            }
            return await next(invocation);
        };
    }

    /// <summary>將驗證錯誤轉換為「JSON 成員路徑（camelCase）→ 穩定規則代碼」之字典。絕不洩漏驗證器原生訊息或使用者提交的原始值。</summary>
    internal static Dictionary<string, string[]> Fields(ValidationResult result) => result.Errors
        .GroupBy(failure => Path(failure.PropertyName), StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.Select(failure => Rule(failure.ErrorCode)).Distinct(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);

    private static string Path(string property) => string.Join('.', property.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));

    // FluentValidation 內建規則回報如 "MaximumLengthValidator"，自動轉為 snake_case；自訂規則則可透過 WithErrorCode 指定明確代碼。
    private static string Rule(string code) => code.EndsWith("Validator", StringComparison.Ordinal)
        ? JsonNamingPolicy.SnakeCaseLower.ConvertName(code[..^"Validator".Length])
        : code;
}
