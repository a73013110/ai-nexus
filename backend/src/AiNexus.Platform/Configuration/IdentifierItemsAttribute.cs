using System.ComponentModel.DataAnnotations;

namespace AiNexus.Platform.Configuration;

/// <summary>Every item of a string list is a 1–64 character identifier of ASCII letters, digits, <c>.</c>, <c>_</c> or <c>-</c> (directory accounts, group ids).</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class IdentifierItemsAttribute : ValidationAttribute
{
    public const int MaxLength = 64;

    public IdentifierItemsAttribute() : base("Every {0} item must be 1–64 ASCII letters, digits, '.', '_' or '-'.") { }

    public override bool IsValid(object? value) => value is null || value is IEnumerable<string> items
        && items.All(x => x is { Length: > 0 and <= MaxLength } && x.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-'));
}
