using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using FluentValidation;

namespace AiNexus.Platform.Validation;

/// <summary>
/// 全專案共用的文字規則，長度一律以去掉前後空白後計算。
/// 單行（名稱、標題、標籤、路徑）不得含任何控制字元：換行會破壞逐行解析與畫面顯示；可換行的內容（內文、說明、備註、留言）只允許換行與 Tab。
/// 驗證器用擴充方法（失敗回 <c>required</c>／<c>too_long</c>／<c>format</c>），實體與 handler 用同一組判斷，規則只有一份。
/// </summary>
public static class TextRules
{
    private static readonly SearchValues<char> LineControls = SearchValues.Create(Controls(allowed: ""));
    private static readonly SearchValues<char> ContentControls = SearchValues.Create(Controls(allowed: "\t\n\r"));

    /// <summary>選填文字的儲存形式：去掉前後空白，空白存成 null。</summary>
    public static string? TrimToNull(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    /// <summary>單行文字含控制字元。</summary>
    public static bool HasLineControl(ReadOnlySpan<char> text) => text.ContainsAny(LineControls);

    /// <summary>可換行的內容含換行與 Tab 以外的控制字元。</summary>
    public static bool HasContentControl(ReadOnlySpan<char> text) => text.ContainsAny(ContentControls);

    /// <summary>必填單行：去掉前後空白後 1 到 <paramref name="maxLength"/> 字，不含控制字元。</summary>
    public static bool IsLine([NotNullWhen(true)] string? text, int maxLength) => Fits(text, maxLength) && !HasLineControl(text);

    /// <summary>必填內容：去掉前後空白後 1 到 <paramref name="maxLength"/> 字，換行與 Tab 以外不含控制字元。</summary>
    public static bool IsContent([NotNullWhen(true)] string? text, int maxLength) => Fits(text, maxLength) && !HasContentControl(text);

    /// <summary>必填單行；規則同 <see cref="IsLine"/>。</summary>
    public static IRuleBuilderOptions<T, string?> RequiredText<T>(this IRuleBuilder<T, string?> rule, int maxLength)
        => rule.NotEmpty().Text(maxLength, LineControls);

    /// <summary>選填單行：null 或空白都可以，有值時規則同 <see cref="RequiredText"/>。</summary>
    public static IRuleBuilderOptions<T, string?> OptionalText<T>(this IRuleBuilder<T, string?> rule, int maxLength) => rule.Text(maxLength, LineControls);

    /// <summary>必填內容；規則同 <see cref="IsContent"/>。</summary>
    public static IRuleBuilderOptions<T, string?> RequiredContent<T>(this IRuleBuilder<T, string?> rule, int maxLength)
        => rule.NotEmpty().Text(maxLength, ContentControls);

    /// <summary>選填內容：null 或空白都可以，有值時規則同 <see cref="RequiredContent"/>。</summary>
    public static IRuleBuilderOptions<T, string?> OptionalContent<T>(this IRuleBuilder<T, string?> rule, int maxLength) => rule.Text(maxLength, ContentControls);

    private static bool Fits([NotNullWhen(true)] string? text, int maxLength) => text is not null && text.AsSpan().Trim().Length is > 0 and var length && length <= maxLength;

    // 預設 cascade 會繼續跑後面的規則，所以每條都要容許 null，空值只回一個 required。
    // 每個請求都會跑：以 span 計算、向量化搜尋，不配置字串也不經過 LINQ。
    private static IRuleBuilderOptions<T, string?> Text<T>(this IRuleBuilder<T, string?> rule, int maxLength, SearchValues<char> controls)
        => rule.Must(x => x is null || x.AsSpan().Trim().Length <= maxLength).WithErrorCode(RuleCode.TooLong)
            .Must(x => x is null || !x.AsSpan().ContainsAny(controls)).WithErrorCode(RuleCode.Format);

    // char.IsControl 只涵蓋 U+0000–U+001F 與 U+007F–U+009F。
    private static string Controls(string allowed)
        => string.Concat(Enumerable.Range(0, 0xA0).Select(x => (char)x).Where(x => char.IsControl(x) && !allowed.Contains(x, StringComparison.Ordinal)));
}
