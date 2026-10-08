using System.Diagnostics.CodeAnalysis;

namespace AiNexus.Platform.Errors;

/// <summary>Outcome of a use case that returns no value. Expected failures are values, not exceptions.</summary>
public readonly record struct Result
{
    private Result(Error? error) => Error = error;

    public Error? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public static Result Success { get; } = new(null);

    public static implicit operator Result(Error error) => new(error ?? throw new ArgumentNullException(nameof(error)));
}

/// <summary>Outcome of a use case: either <see cref="Value"/> or an expected <see cref="Error"/>.</summary>
public readonly record struct Result<T>
{
    private Result(T? value, Error? error) => (Value, Error) = (value, error);

    public T? Value { get; }
    public Error? Error { get; }

    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    /// <summary>Success. Needed where <typeparamref name="T"/> is an interface, which C# never converts implicitly.</summary>
    public static Result<T> Ok(T value) => new(value ?? throw new ArgumentNullException(nameof(value)), null);

    public static implicit operator Result<T>(T value) => Ok(value);
    public static implicit operator Result<T>(Error error) => new(default, error ?? throw new ArgumentNullException(nameof(error)));
}
