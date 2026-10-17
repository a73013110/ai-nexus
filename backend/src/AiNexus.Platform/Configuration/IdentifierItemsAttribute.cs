using System.ComponentModel.DataAnnotations;

namespace AiNexus.Platform.Configuration;

/// <summary>字串清單中的每個元素，都必須是 1–64 個字元的 ASCII 英數字，或 <c>.</c>、<c>_</c>、<c>-</c> 組成的識別碼（如帳號、群組代碼）。</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class IdentifierItemsAttribute : ValidationAttribute
{
    public const int MaxLength = 64;

    public IdentifierItemsAttribute() : base("Every {0} item must be 1–64 ASCII letters, digits, '.', '_' or '-'.") { }

    public override bool IsValid(object? value) => value is null || value is IEnumerable<string> items
        && items.All(x => x is { Length: > 0 and <= MaxLength } && x.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-'));
}
