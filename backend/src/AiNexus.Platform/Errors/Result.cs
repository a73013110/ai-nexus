using System.Diagnostics.CodeAnalysis;

namespace AiNexus.Platform.Errors;

/// <summary>無回傳值的使用案例執行結果。預期內的失敗以資料回傳，不拋出例外。</summary>
public readonly record struct Result
{
    private Result(Error? error) => Error = error;

    public Error? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public static Result Success { get; } = new(null);

    public static implicit operator Result(Error error) => new(error ?? throw new ArgumentNullException(nameof(error)));
}

/// <summary>有回傳值的使用案例執行結果：成功時為 <see cref="Value"/>，預期內的業務失敗則為 <see cref="Error"/>。</summary>
public readonly record struct Result<T>
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
}
