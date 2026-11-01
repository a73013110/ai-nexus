using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace AiNexus.Platform.Data.Tables;

/// <summary>
/// 一個宣告過的欄位。值的型別在宣告時（<see cref="TableColumns{T}"/> 的型別參數）就確定，
/// 排序與篩選都在 <see cref="TableColumn{T, TValue}"/> 以強型別建立，請求時不必以反射找方法或建立泛型型別。
/// </summary>
internal abstract class TableColumn<T>(TableFilterKind kind, TableUse use)
{
    public TableFilterKind Kind { get; } = kind;

    public TableUse Use { get; } = use;

    public abstract IOrderedQueryable<T> OrderBy(IQueryable<T> query, bool descending);

    public abstract IOrderedQueryable<T> ThenBy(IOrderedQueryable<T> query);

    /// <summary>欄位種類不支援這個運算，或值的格式不對時回傳 null。</summary>
    public abstract Expression<Func<T, bool>>? Predicate(string operation, string value);
}

internal sealed class TableColumn<T, TValue>(TableFilterKind kind, Expression<Func<T, TValue>> value, TableUse use) : TableColumn<T>(kind, use)
{
    private const int MaxOptions = 100;

    // 以方法群組轉成委派取得 MethodInfo：多載由編譯器解析，不必依名稱與參數數量搜尋，也不必 MakeGenericMethod。
    private static readonly MethodInfo StringContains = new Func<string, bool>(string.Empty.Contains).Method;
    private static readonly MethodInfo EnumerableContains = new Func<IEnumerable<TValue>, TValue, bool>(Enumerable.Contains).Method;

    public override IOrderedQueryable<T> OrderBy(IQueryable<T> query, bool descending) => descending ? query.OrderByDescending(value) : query.OrderBy(value);

    public override IOrderedQueryable<T> ThenBy(IOrderedQueryable<T> query) => query.ThenBy(value);

    public override Expression<Func<T, bool>>? Predicate(string operation, string text)
    {
        var body = value.Body;
        Expression? condition = (Kind, operation) switch
        {
            // Text 欄位由 TableColumns.Text 宣告，值一定是 string。
            (TableFilterKind.Text, "has") => Expression.AndAlso(
                Expression.NotEqual(body, Expression.Constant(null, typeof(string))),
                Expression.Call(body, StringContains, Parameter(text))),
            (TableFilterKind.Options, "in") => Options(body, text),
            (TableFilterKind.Range, "ge") => Compare(body, text, Expression.GreaterThanOrEqual),
            (TableFilterKind.Range, "le") => Compare(body, text, Expression.LessThanOrEqual),
            (TableFilterKind.Range, "lt") => Compare(body, text, Expression.LessThan),
            _ => null,
        };
        return condition is null ? null : Expression.Lambda<Func<T, bool>>(condition, value.Parameters);
    }

    private static MethodCallExpression? Options(Expression body, string text)
    {
        var texts = text.Split(',');
        if (texts.Length > MaxOptions) return null;
        var values = new List<TValue>(texts.Length);
        foreach (var item in texts)
        {
            if (!TryConvert(item, out var parsed)) return null;
            values.Add(parsed);
        }
        return Expression.Call(EnumerableContains, Parameter(values), body);
    }

    // 文字只能比對是否包含，不支援範圍。
    private static BinaryExpression? Compare(Expression body, string text, Func<Expression, Expression, BinaryExpression> compare) =>
        typeof(TValue) != typeof(string) && TryConvert(text, out var bound) ? compare(body, Parameter(bound)) : null;

    /// <summary>
    /// 經由閉包取值：編譯器產生的是對閉包欄位的成員存取，EF 會把它當成查詢參數送出，而不是把值嵌進 SQL（不可改成 <see cref="Expression.Constant(object)"/>）。
    /// </summary>
    private static MemberExpression Parameter<TArgument>(TArgument argument) => (MemberExpression)((Expression<Func<TArgument>>)(() => argument)).Body;

    /// <summary>依欄位型別解析篩選值；不支援的型別或格式不對時失敗。</summary>
    private static bool TryConvert(string text, out TValue value)
    {
        var target = Nullable.GetUnderlyingType(typeof(TValue)) ?? typeof(TValue);
        var invariant = CultureInfo.InvariantCulture;
        object? parsed = null;
        if (target == typeof(string)) parsed = text;
        else if (target == typeof(bool) && bool.TryParse(text, out var flag)) parsed = flag;
        else if (target == typeof(Guid) && Guid.TryParse(text, out var guid)) parsed = guid;
        else if (target.IsEnum && Enum.TryParse(target, text, ignoreCase: true, out var member) && Enum.IsDefined(target, member!)) parsed = member;
        else if (target == typeof(int) && int.TryParse(text, NumberStyles.Integer, invariant, out var integer)) parsed = integer;
        else if (target == typeof(long) && long.TryParse(text, NumberStyles.Integer, invariant, out var big)) parsed = big;
        else if (target == typeof(decimal) && decimal.TryParse(text, NumberStyles.Number, invariant, out var money)) parsed = money;
        else if (target == typeof(double) && double.TryParse(text, NumberStyles.Float, invariant, out var real) && double.IsFinite(real)) parsed = real;
        else if (target == typeof(DateOnly) && DateOnly.TryParseExact(text, "yyyy-MM-dd", invariant, DateTimeStyles.None, out var day)) parsed = day;
        else if (target == typeof(DateTimeOffset) && DateTimeOffset.TryParse(text, invariant, DateTimeStyles.AssumeUniversal, out var instant)) parsed = instant;
        // 拆箱成 TValue（可為 null 的型別也接受底層型別的值）。
        value = parsed is null ? default! : (TValue)parsed;
        return parsed is not null;
    }
}
