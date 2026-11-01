using System.Text.RegularExpressions;
using FluentValidation;

namespace AiNexus.Platform.Validation;

/// <summary>
/// 欄位規則代碼：驗證失敗時寫在 problem 的 <c>errors</c>（欄位 → 代碼）。代碼是公開合約，不跟著成員改名。
/// 兩個以上模組用到的列在這裡；只有一個功能用到的，在該驗證器宣告成具名欄位（<c>static readonly RuleCode Window = new("window")</c>），
/// 第二個模組也需要時再搬到這裡。字串版 <c>WithErrorCode</c> 已禁用（BannedSymbols），代碼只能經由這個型別指定。
/// </summary>
public sealed partial record RuleCode
{
    /// <summary>必填卻沒有值（含只有空白）。</summary>
    public static readonly RuleCode Required = new("required");
    /// <summary>這個情況下不該有值（例如群組項目帶了網址）。</summary>
    public static readonly RuleCode NotAllowed = new("not_allowed");
    /// <summary>格式或字元不符（含控制字元）。</summary>
    public static readonly RuleCode Format = new("format");
    /// <summary>內容無法解析或不符合該型別的規則。</summary>
    public static readonly RuleCode Invalid = new("invalid");
    /// <summary>長度不在範圍內（同時涵蓋空白與過長）。</summary>
    public static readonly RuleCode Length = new("length");
    /// <summary>超過長度上限。</summary>
    public static readonly RuleCode TooLong = new("too_long");
    /// <summary>數值不在範圍內。</summary>
    public static readonly RuleCode Range = new("range");
    /// <summary>項目數不在範圍內（同時涵蓋太少與太多）。</summary>
    public static readonly RuleCode Count = new("count");
    /// <summary>項目數超過上限。</summary>
    public static readonly RuleCode TooMany = new("too_many");
    /// <summary>清單內有重複項目。</summary>
    public static readonly RuleCode Duplicate = new("duplicate");
    /// <summary>不在允許的值之中。</summary>
    public static readonly RuleCode Unknown = new("unknown");

    /// <param name="value">snake_case；不符時丟例外，驗證器在啟動建立時就會失敗。</param>
    public RuleCode(string value)
    {
        if (!ValuePattern().IsMatch(value)) throw new ArgumentException($"欄位規則代碼必須是 snake_case：{value}", nameof(value));
        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;

    [GeneratedRegex("^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$")]
    private static partial Regex ValuePattern();
}

/// <summary>以 <see cref="RuleCode"/> 指定規則代碼，與跨模組共用的文字規則。</summary>
public static class RuleCodeExtensions
{
    /// <summary>指定這條規則失敗時回傳的代碼。</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("ApiDesign", "RS0030", Justification = "字串版只在這裡呼叫，其他地方經由 RuleCode 指定代碼。")]
    public static IRuleBuilderOptions<T, TProperty> WithErrorCode<T, TProperty>(this IRuleBuilderOptions<T, TProperty> rule, RuleCode code)
        => DefaultValidatorOptions.WithErrorCode(rule, code.Value);

    /// <summary>必填文字：不可空白；去掉前後空白後不超過 <paramref name="maxLength"/>、不含控制字元（會破壞逐行解析與畫面顯示）。</summary>
    public static IRuleBuilderOptions<T, string?> RequiredText<T>(this IRuleBuilder<T, string?> rule, int maxLength)
        => rule.Must(x => !string.IsNullOrWhiteSpace(x)).WithErrorCode(RuleCode.Required).TrimmedText(maxLength);

    /// <summary>選填文字：null 或空白都可以；有值時規則同 <see cref="RequiredText"/>。</summary>
    public static IRuleBuilderOptions<T, string?> OptionalText<T>(this IRuleBuilder<T, string?> rule, int maxLength) => rule.TrimmedText(maxLength);

    // 預設 cascade 會繼續跑後面的規則，所以每條都要容許 null，空值只回一個 required。
    private static IRuleBuilderOptions<T, string?> TrimmedText<T>(this IRuleBuilder<T, string?> rule, int maxLength)
        => rule.Must(x => x is null || x.Trim().Length <= maxLength).WithErrorCode(RuleCode.TooLong)
            .Must(x => x is null || !x.Any(char.IsControl)).WithErrorCode(RuleCode.Format);
}
