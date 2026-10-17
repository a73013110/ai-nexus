using System.Diagnostics.CodeAnalysis;

namespace AiNexus.Platform.Errors;

/// <summary>無回傳值之使用案例的執行結果。預期內的失敗皆為純資料值，而非例外狀況。</summary>
public readonly record struct Result
{
    private Result(Error? error) => Error = error;

    public Error? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public static Result Success { get; } = new(null);

    public static implicit operator Result(Error error) => new(error ?? throw new ArgumentNullException(nameof(error)));
}

/// <summary>具回傳值之使用案例的執行結果：包含成功產物 <see cref="Value"/> 或預期內的業務失敗 <see cref="Error"/>。</summary>
public readonly record struct Result<T>
{
    private Result(T? value, Error? error) => (Value, Error) = (value, error);

    public T? Value { get; }
    public Error? Error { get; }

    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    /// <summary>建立成功結果。在 <typeparamref name="T"/> 為介面型別時特別需要（C# 不支援隱式轉換至介面）。</summary>
    public static Result<T> Ok(T value) => new(value ?? throw new ArgumentNullException(nameof(value)), null);

    public static implicit operator Result<T>(T value) => Ok(value);
    public static implicit operator Result<T>(Error error) => new(default, error ?? throw new ArgumentNullException(nameof(error)));
}
