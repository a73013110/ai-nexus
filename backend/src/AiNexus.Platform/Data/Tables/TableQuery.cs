using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Platform.Data.Tables;

/// <summary>
/// 已依 <see cref="TableColumns{T}"/> 驗證的表格查詢：篩選、排序與分頁。列表端點通常直接用 <see cref="TableQueryExtensions.ToTablePageAsync"/>；
/// 匯出等不分頁的讀取用 <see cref="TableQueryExtensions.Parse"/> 取得後自行套用 <see cref="Filter"/> 與 <see cref="Order"/>，結果才會與畫面一致。
/// 比對值都以參數送出（不嵌進 SQL），查詢計畫可重複使用。
/// </summary>
public sealed class TableQuery<T>
{
    private const int DefaultLimit = 50;
    private const int MaxFilters = 20;
    private const int MaxValueLength = 200;
    private const int MaxOptions = 100;

    private readonly IReadOnlyList<Expression<Func<T, bool>>> filters;
    private readonly LambdaExpression? sort;
    private readonly bool descending;
    private readonly LambdaExpression key;

    internal TableQuery(IReadOnlyList<Expression<Func<T, bool>>> filters, LambdaExpression? sort, bool descending, LambdaExpression key, int offset, int limit) =>
        (this.filters, this.sort, this.descending, this.key, Offset, Limit) = (filters, sort, descending, key, offset, limit);

    public int Offset { get; }
    public int Limit { get; }
    public bool Filtered => filters.Count > 0;

    public IQueryable<T> Filter(IQueryable<T> query) => filters.Aggregate(query, (current, filter) => current.Where(filter));

    /// <summary>依指定（或預設）欄位排序，最後一定以唯一鍵排序，同值的列在每次查詢都是同一個次序。</summary>
    public IQueryable<T> Order(IQueryable<T> query)
    {
        if (sort is null) return OrderBy(query, key, nameof(Queryable.OrderBy));
        var ordered = OrderBy(query, sort, descending ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy));
        return OrderBy(ordered, key, nameof(Queryable.ThenBy));
    }

    internal static Result<TableQuery<T>> Parse(TableColumns<T> columns, TableRequest request)
    {
        var key = columns.KeyExpression ?? throw new InvalidOperationException($"{typeof(T).Name} 的表格欄位沒有指定唯一鍵。");
        var limit = request.Limit ?? DefaultLimit;
        var offset = request.Offset ?? 0;
        if (offset < 0 || limit < 1 || limit > columns.MaxLimit) return TableQueryExtensions.Invalid;

        LambdaExpression? sort = columns.DefaultColumn is { } fallback ? columns.Find(fallback)!.Value : null;
        var descending = columns.DefaultDescending;
        if (!string.IsNullOrEmpty(request.Sort))
        {
            var parts = request.Sort.Split(':');
            if (parts.Length > 2 || columns.Find(parts[0]) is not { } column || !column.Use.HasFlag(TableUse.Sort)) return TableQueryExtensions.Invalid;
            if (parts.Length == 2 && parts[1] is not ("asc" or "desc")) return TableQueryExtensions.Invalid;
            (sort, descending) = (column.Value, parts.Length == 2 && parts[1] == "desc");
        }

        var filters = new List<Expression<Func<T, bool>>>();
        var raw = request.Filter ?? [];
        if (raw.Length > MaxFilters) return TableQueryExtensions.Invalid;
        foreach (var text in raw)
        {
            // 只切前兩個冒號：文字包含的值本身可能有冒號。
            var parts = text.Split(':', 3);
            if (parts.Length != 3 || parts[2].Length is 0 or > MaxValueLength
                || columns.Find(parts[0]) is not { } column || !column.Use.HasFlag(TableUse.Filter)
                || Predicate(column, parts[1], parts[2]) is not { } predicate)
                return TableQueryExtensions.Invalid;
            filters.Add(predicate);
        }
        return new TableQuery<T>(filters, sort, descending, key, offset, limit);
    }

    private static Expression<Func<T, bool>>? Predicate(TableColumnDefinition column, string operation, string value)
    {
        var parameter = column.Value.Parameters[0];
        var body = column.Value.Body;
        var type = body.Type;
        Expression? condition = (column.Kind, operation) switch
        {
            (TableFilterKind.Text, "has") => Expression.AndAlso(
                Expression.NotEqual(body, Expression.Constant(null, typeof(string))),
                Expression.Call(body, StringContains, Parameter(value, typeof(string)))),
            (TableFilterKind.Options, "in") => OptionsCondition(body, value),
            (TableFilterKind.Range, "ge") => Compare(body, value, Expression.GreaterThanOrEqual),
            (TableFilterKind.Range, "le") => Compare(body, value, Expression.LessThanOrEqual),
            (TableFilterKind.Range, "lt") => Compare(body, value, Expression.LessThan),
            _ => null,
        };
        return condition is null || (column.Kind == TableFilterKind.Range && type == typeof(string))
            ? null
            : Expression.Lambda<Func<T, bool>>(condition, parameter);
    }

    private static Expression? OptionsCondition(Expression body, string value)
    {
        var texts = value.Split(',');
        if (texts.Length > MaxOptions) return null;
        var list = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(body.Type))!;
        foreach (var text in texts)
        {
            if (!TryConvert(text, body.Type, out var item)) return null;
            list.Add(item);
        }
        var contains = EnumerableContains.MakeGenericMethod(body.Type);
        return Expression.Call(contains, Parameter(list, list.GetType()), body);
    }

    private static Expression? Compare(Expression body, string value, Func<Expression, Expression, BinaryExpression> compare) =>
        TryConvert(value, body.Type, out var item) && item is not string ? compare(body, Parameter(item, body.Type)) : null;

    /// <summary>依屬性型別解析篩選值；不支援的型別或格式不對時失敗。</summary>
    private static bool TryConvert(string text, Type type, out object? value)
    {
        var target = Nullable.GetUnderlyingType(type) ?? type;
        var invariant = CultureInfo.InvariantCulture;
        value = null;
        if (target == typeof(string)) value = text;
        else if (target == typeof(bool) && bool.TryParse(text, out var flag)) value = flag;
        else if (target == typeof(Guid) && Guid.TryParse(text, out var guid)) value = guid;
        else if (target.IsEnum && Enum.TryParse(target, text, ignoreCase: true, out var member) && Enum.IsDefined(target, member!)) value = member;
        else if (target == typeof(int) && int.TryParse(text, NumberStyles.Integer, invariant, out var integer)) value = integer;
        else if (target == typeof(long) && long.TryParse(text, NumberStyles.Integer, invariant, out var big)) value = big;
        else if (target == typeof(decimal) && decimal.TryParse(text, NumberStyles.Number, invariant, out var money)) value = money;
        else if (target == typeof(double) && double.TryParse(text, NumberStyles.Float, invariant, out var real) && double.IsFinite(real)) value = real;
        else if (target == typeof(DateOnly) && DateOnly.TryParseExact(text, "yyyy-MM-dd", invariant, DateTimeStyles.None, out var day)) value = day;
        else if (target == typeof(DateTimeOffset) && DateTimeOffset.TryParse(text, invariant, DateTimeStyles.AssumeUniversal, out var instant)) value = instant;
        return value is not null;
    }

    /// <summary>包成成員存取，EF 才會把值當參數送出，而不是嵌進 SQL 字串。</summary>
    private static MemberExpression Parameter(object? value, Type type)
    {
        var holder = Activator.CreateInstance(typeof(Holder<>).MakeGenericType(type), value)!;
        return Expression.Property(Expression.Constant(holder), nameof(Holder<object>.Value));
    }

    private static IQueryable<T> OrderBy(IQueryable<T> query, LambdaExpression selector, string method) =>
        (IQueryable<T>)typeof(Queryable).GetMethods()
            .Single(x => x.Name == method && x.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(T), selector.ReturnType)
            .Invoke(null, [query, selector])!;

    private static readonly MethodInfo StringContains = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;
    private static readonly MethodInfo EnumerableContains = typeof(Enumerable).GetMethods()
        .Single(x => x.Name == nameof(Enumerable.Contains) && x.GetParameters().Length == 2);
}

/// <summary>篩選值的容器：EF 把對它的成員存取當成查詢參數。</summary>
internal sealed class Holder<TValue>(TValue value)
{
    public TValue Value { get; } = value;
}

/// <summary>以 <see cref="TableColumns{T}"/> 讀出表格的一頁。</summary>
public static class TableQueryExtensions
{
    /// <summary>排序或篩選用了未宣告的欄位、不支援的運算或格式不對的值。</summary>
    public static readonly Error Invalid = Error.Invalid("invalid_table_query");

    /// <summary>驗證查詢參數；匯出等不分頁的讀取用它套用與畫面相同的篩選與排序。</summary>
    public static Result<TableQuery<T>> Parse<T>(this TableColumns<T> columns, TableRequest request) => TableQuery<T>.Parse(columns, request);

    /// <summary>
    /// 符合 <paramref name="query"/> 的資料不超過 <see cref="TableColumns{T}.CompleteUpTo"/> 時，忽略欄位篩選與分頁、依排序回傳全部（<c>Complete</c>）；
    /// 否則套用欄位篩選、排序與分頁，<c>Total</c> 是篩選後的總數。
    /// <c>select</c> 把已排序（與分頁）的查詢投影成 DTO，例如 <c>q =&gt; q.Select(...)</c> 或模組的 <c>ToDtos</c>；投影保留排序。
    /// </summary>
    public static async Task<Result<TablePage<TResult>>> ToTablePageAsync<T, TResult>(
        this IQueryable<T> query, TableRequest request, TableColumns<T> columns, Func<IQueryable<T>, IQueryable<TResult>> select, CancellationToken ct)
    {
        var parsed = columns.Parse(request);
        if (!parsed.IsSuccess) return parsed.Error;
        var table = parsed.Value;
        var total = await query.CountAsync(ct);
        if (total <= columns.CompleteUpTo)
            return new TablePage<TResult>(await select(table.Order(query)).ToListAsync(ct), total, 0, true);
        var filtered = table.Filter(query);
        if (table.Filtered) total = await filtered.CountAsync(ct);
        var items = await select(table.Order(filtered).Skip(table.Offset).Take(table.Limit)).ToListAsync(ct);
        return new TablePage<TResult>(items, total, table.Offset, false);
    }
}
