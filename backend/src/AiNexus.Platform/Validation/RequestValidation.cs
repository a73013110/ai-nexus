using System.Text.Json;
using AiNexus.Platform.Errors;
using FluentValidation;
using FluentValidation.Results;

namespace AiNexus.Platform.Validation;

/// <summary>Base for request validators. Override <see cref="ProblemCode"/> to keep a feature's established public code.</summary>
public abstract class RequestValidator<T> : AbstractValidator<T>, IRequestValidator
{
    public virtual string ProblemCode => RequestValidation.DefaultCode;
}

public interface IRequestValidator
{
    string ProblemCode { get; }
}

/// <summary>
/// Validates every endpoint argument that has an <see cref="IValidator{T}"/> before the handler runs. Applied once to a
/// route group, so a new endpoint cannot forget it; endpoints without validated arguments get no filter at all.
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

    /// <summary>JSON member path → stable rule codes. Never the validator's message or the submitted value.</summary>
    internal static Dictionary<string, string[]> Fields(ValidationResult result) => result.Errors
        .GroupBy(failure => Path(failure.PropertyName), StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.Select(failure => Rule(failure.ErrorCode)).Distinct(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);

    private static string Path(string property) => string.Join('.', property.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));

    // Built-in rules report e.g. "MaximumLengthValidator"; custom rules set an explicit snake_case code with WithErrorCode.
    private static string Rule(string code) => code.EndsWith("Validator", StringComparison.Ordinal)
        ? JsonNamingPolicy.SnakeCaseLower.ConvertName(code[..^"Validator".Length])
        : code;
}
