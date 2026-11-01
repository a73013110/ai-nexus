using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Metadata;

namespace AiNexus.Platform.Errors;

/// <summary>
/// 無回傳值的使用案例執行結果。預期內的失敗以資料回傳，不拋出例外。
/// 端點可直接回傳它：成功 204、失敗 problem；<see cref="INestedHttpResult"/> 讓請求稽核取得 problem 的錯誤代碼，OpenAPI 與 <c>Results&lt;NoContent, ProblemHttpResult&gt;</c> 相同。
/// 是 class 而不是 struct：RDG 對回傳 <see cref="IResult"/> 的端點一律產生 null 檢查，struct 編譯不過；經過端點過濾器時 struct 也會裝箱，省不到配置。
/// </summary>
public sealed record Result : IResult, INestedHttpResult, IEndpointMetadataProvider
{
    private Result(Error? error) => Error = error;

    public Error? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public static Result Success { get; } = new((Error?)null);

    public static implicit operator Result(Error error) => new(error ?? throw new ArgumentNullException(nameof(error)));

    IResult INestedHttpResult.Result => this.ToHttpResult();

    Task IResult.ExecuteAsync(HttpContext httpContext) => this.ToHttpResult().ExecuteAsync(httpContext);

    static void IEndpointMetadataProvider.PopulateMetadata(MethodInfo method, EndpointBuilder builder)
        => Problems.PopulateMetadata<Results<NoContent, ProblemHttpResult>>(method, builder);
}

/// <summary>
/// 有回傳值的使用案例執行結果：成功時為 <see cref="Value"/>，預期內的業務失敗則為 <see cref="Error"/>。
/// 端點可直接回傳它：成功 200、失敗 problem；其餘同 <see cref="Result"/>。成功不是 200 時用 <c>ToHttpResultAsync(mapper)</c>。
/// </summary>
public sealed record Result<T> : IResult, INestedHttpResult, IEndpointMetadataProvider
{
    private Result(T? value, Error? error) => (Value, Error) = (value, error);

    public T? Value { get; }
    public Error? Error { get; }

    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    /// <summary>建立成功結果。<typeparamref name="T"/> 為介面型別時必須用此方法，因為 C# 不支援隱式轉換成介面。</summary>
    public static Result<T> Ok(T value) => new(value ?? throw new ArgumentNullException(nameof(value)), null);

    public static implicit operator Result<T>(T value) => Ok(value);
    public static implicit operator Result<T>(Error error) => new(default, error ?? throw new ArgumentNullException(nameof(error)));

    IResult INestedHttpResult.Result => this.ToHttpResult();

    Task IResult.ExecuteAsync(HttpContext httpContext) => this.ToHttpResult().ExecuteAsync(httpContext);

    static void IEndpointMetadataProvider.PopulateMetadata(MethodInfo method, EndpointBuilder builder)
        => Problems.PopulateMetadata<Results<Ok<T>, ProblemHttpResult>>(method, builder);
}
