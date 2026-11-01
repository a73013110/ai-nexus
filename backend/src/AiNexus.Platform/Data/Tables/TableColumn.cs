using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace AiNexus.Platform.Data.Tables;

/// <summary>
/// 依一個運算式排序。唯一鍵與可排序的欄位共用；值的型別在宣告時就確定，請求時不必以反射找方法或建立泛型型別。
/// </summary>
internal abstract class TableOrder<T>
{
    public abstract IOrderedQueryable<T> OrderBy(IQueryable<T> query, bool descending);

    public abstract IOrderedQueryable<T> ThenBy(IOrderedQueryable<T> query);
}

internal sealed class TableOrder<T, TValue>(Expression<Func<T, TValue>> value) : TableOrder<T>
{
    public override IOrderedQueryable<T> OrderBy(IQueryable<T> query, bool descending) => descending ? query.OrderByDescending(value) : query.OrderBy(value);

    public override IOrderedQueryable<T> ThenBy(IOrderedQueryable<T> query) => query.ThenBy(value);
}

/// <summary>一個宣告過的欄位：排序方式、篩選種類與用途。篩選條件在 <see cref="TableColumn{T, TValue}"/> 以強型別建立。</summary>
internal abstract class TableColumn<T>(TableFilterKind kind, TableUse use, TableOrder<T> order)
{
    public TableFilterKind Kind { get; } = kind;

    public TableUse Use { get; } = use;

    public TableOrder<T> Order { get; } = order;

    /// <summary>欄位種類不支援這個運算，或值的格式不對時回傳 null。</summary>
    public abstract Expression<Func<T, bool>>? Predicate(string operation, string value);
}

internal sealed class TableColumn<T, TValue> : TableColumn<T>
{
    private const int MaxOptions = 100;

    // 以方法群組轉成委派取得 MethodInfo：多載由編譯器解析，不必依名稱與參數數量搜尋，也不必 MakeGenericMethod。
    private static readonly MethodInfo StringContains = new Func<string, bool>(string.Empty.Contains).Method;
    private static readonly MethodInfo EnumerableContains = new Func<IEnumerable<TValue>, TValue, bool>(Enumerable.Contains).Method;

    // 每個 TValue 只決定一次解析方式；null 表示這個型別不能當篩選值。
    private static readonly TryParse? Parse = ParserFor(Nullable.GetUnderlyingType(typeof(TValue)) ?? typeof(TValue));

    private readonly Expression<Func<T, TValue>> value;

    public TableColumn(string id, TableFilterKind kind, Expression<Func<T, TValue>> value, TableUse use) : base(kind, use, new TableOrder<T, TValue>(value))
    {
        this.value = value;
        // 宣告時就擋下篩選不了的型別：否則欄位照樣宣告成功，但每次篩選都只會回 invalid_table_query，或在建立運算式時拋出例外。
        if (use.HasFlag(TableUse.Filter) && !Supports(kind))
            throw new ArgumentException($"欄位 {id} 的型別 {typeof(TValue).Name} 不支援 {kind} 篩選。", nameof(value));
    }

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

    private static bool Supports(TableFilterKind kind) => kind switch
    {
        TableFilterKind.Text => typeof(TValue) == typeof(string),
        TableFilterKind.Options => Parse is not null,
        // 範圍只用在有大小順序、運算式樹支援比較運算子的型別（列舉、布林、Guid、字串都沒有）。
        TableFilterKind.Range => (Nullable.GetUnderlyingType(typeof(TValue)) ?? typeof(TValue)) is var target
            && (target == typeof(int) || target == typeof(long) || target == typeof(decimal) || target == typeof(double)
                || target == typeof(DateOnly) || target == typeof(DateTimeOffset)),
        _ => false,
    };

    private static MethodCallExpression? Options(Expression body, string text)
    {
        var texts = text.Split(',');
        if (texts.Length > MaxOptions) return null;
        var values = new List<TValue>(texts.Length);
        foreach (var item in texts)
        {
            if (!Parse!(item, out var parsed)) return null;
            values.Add(parsed);
        }
        return Expression.Call(EnumerableContains, Parameter(values), body);
    }

    private static BinaryExpression? Compare(Expression body, string text, Func<Expression, Expression, BinaryExpression> compare) =>
        Parse!(text, out var bound) ? compare(body, Parameter(bound)) : null;

    /// <summary>
    /// 經由閉包取值：編譯器產生的是對閉包欄位的成員存取，EF 會把它當成查詢參數送出，而不是把值嵌進 SQL（不可改成 <see cref="Expression.Constant(object)"/>）。
    /// </summary>
    private static MemberExpression Parameter<TArgument>(TArgument argument) => (MemberExpression)((Expression<Func<TArgument>>)(() => argument)).Body;

    private delegate bool TryParse(string text, out TValue value);

    /// <summary>依欄位的底層型別選擇解析方式；格式固定（不依目前文化），不支援的型別回傳 null。</summary>
    private static TryParse? ParserFor(Type target)
    {
        var invariant = CultureInfo.InvariantCulture;
        if (target == typeof(string)) return (string text, out TValue value) => Set(true, text, out value);
        if (target == typeof(bool)) return (string text, out TValue value) => Set(bool.TryParse(text, out var parsed), parsed, out value);
        if (target == typeof(Guid)) return (string text, out TValue value) => Set(Guid.TryParse(text, out var parsed), parsed, out value);
        if (target.IsEnum)
            return (string text, out TValue value) => Set(Enum.TryParse(target, text, ignoreCase: true, out var parsed) && Enum.IsDefined(target, parsed!), parsed, out value);
        if (target == typeof(int)) return (string text, out TValue value) => Set(int.TryParse(text, NumberStyles.Integer, invariant, out var parsed), parsed, out value);
        if (target == typeof(long)) return (string text, out TValue value) => Set(long.TryParse(text, NumberStyles.Integer, invariant, out var parsed), parsed, out value);
        if (target == typeof(decimal)) return (string text, out TValue value) => Set(decimal.TryParse(text, NumberStyles.Number, invariant, out var parsed), parsed, out value);
        if (target == typeof(double))
            return (string text, out TValue value) => Set(double.TryParse(text, NumberStyles.Float, invariant, out var parsed) && double.IsFinite(parsed), parsed, out value);
        if (target == typeof(DateOnly))
            return (string text, out TValue value) => Set(DateOnly.TryParseExact(text, "yyyy-MM-dd", invariant, DateTimeStyles.None, out var parsed), parsed, out value);
        if (target == typeof(DateTimeOffset))
            return (string text, out TValue value) => Set(DateTimeOffset.TryParse(text, invariant, DateTimeStyles.AssumeUniversal, out var parsed), parsed, out value);
        return null;
    }

    // 轉成 TValue（可為 null 的型別也接受底層型別的值）。
    private static bool Set<TParsed>(bool ok, TParsed parsed, out TValue value)
    {
        value = ok ? (TValue)(object)parsed! : default!;
        return ok;
    }
}
