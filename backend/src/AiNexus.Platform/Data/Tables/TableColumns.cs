using System.Linq.Expressions;

namespace AiNexus.Platform.Data.Tables;

/// <summary>欄位能做的事；預設可排序也可篩選。</summary>
[Flags]
public enum TableUse
{
    Sort = 1,
    Filter = 2,
    All = Sort | Filter,
}

/// <summary>篩選運算的種類：文字包含、選項（列舉、布林、字串代碼）或範圍（數字、日期）。</summary>
public enum TableFilterKind
{
    Text,
    Options,
    Range,
}

/// <summary>
/// 一個列表端點對外的欄位白名單（ADR 0026）：欄位 ID 對應到實體上的運算式，查詢只接受這裡宣告的欄位與運算。
/// 必須以 <see cref="Key"/> 指定唯一鍵，作為同值時的固定次序，換頁時才不會重複或漏掉列。
/// <see cref="CompleteUpTo"/> 是「一次回傳全部」的門檻：符合查詢條件的資料不超過它時，排序、篩選與分頁交給前端。
/// </summary>
public sealed class TableColumns<T>
{
    private readonly Dictionary<string, TableColumnDefinition> columns = new(StringComparer.Ordinal);

    internal LambdaExpression? KeyExpression { get; private set; }
    internal string? DefaultColumn { get; private set; }
    internal bool DefaultDescending { get; private set; }

    /// <summary>資料筆數不超過這個值時一次回傳全部；0 表示永遠逐頁讀取（每列很寬或附帶大量關聯資料時）。</summary>
    public int CompleteUpTo { get; init; } = 500;

    /// <summary>每頁筆數上限；前端可選的每頁筆數不能超過它。</summary>
    public int MaxLimit { get; init; } = 200;

    public TableColumns<T> Text(string id, Expression<Func<T, string?>> value, TableUse use = TableUse.All) =>
        Add(id, TableFilterKind.Text, value, use);

    /// <summary>以值清單篩選的欄位；值依屬性型別解析（列舉用名稱、布林用 true／false）。</summary>
    public TableColumns<T> Options<TValue>(string id, Expression<Func<T, TValue>> value, TableUse use = TableUse.All) =>
        Add(id, TableFilterKind.Options, value, use);

    /// <summary>以範圍篩選的欄位（整數、小數、<see cref="DateTimeOffset"/>、<see cref="DateOnly"/>，可為 null）。</summary>
    public TableColumns<T> Range<TValue>(string id, Expression<Func<T, TValue>> value, TableUse use = TableUse.All) =>
        Add(id, TableFilterKind.Range, value, use);

    public TableColumns<T> Key<TKey>(Expression<Func<T, TKey>> key)
    {
        KeyExpression = key;
        return this;
    }

    /// <summary>沒有指定排序時的次序；欄位必須已宣告。</summary>
    public TableColumns<T> DefaultSort(string id, bool descending = false)
    {
        if (!columns.ContainsKey(id)) throw new ArgumentException($"未宣告的欄位：{id}", nameof(id));
        (DefaultColumn, DefaultDescending) = (id, descending);
        return this;
    }

    internal TableColumnDefinition? Find(string id) => columns.GetValueOrDefault(id);

    private TableColumns<T> Add(string id, TableFilterKind kind, LambdaExpression value, TableUse use)
    {
        if (!columns.TryAdd(id, new TableColumnDefinition(kind, value, use)))
            throw new ArgumentException($"欄位重複：{id}", nameof(id));
        return this;
    }
}

internal sealed record TableColumnDefinition(TableFilterKind Kind, LambdaExpression Value, TableUse Use);
