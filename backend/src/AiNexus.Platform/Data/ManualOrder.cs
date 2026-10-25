namespace AiNexus.Platform.Data;

/// <summary>
/// 手動排序的值計算（規範見 docs/architecture/manual-ordering.md；前端整份清單在畫面上時用同一套規則 <c>shared/browser/sort-order.ts</c>）。
/// 存整數排序值、值之間保留間隔，一次移動通常只改被移動的那一筆；null 是未排序，只能排在已排序者之後。
/// </summary>
public static class ManualOrder
{
    /// <summary>預設間隔：新值取前後的中間，末端加 10，沒有空間時整段重新編號為 10、20、30…。</summary>
    public const int Step = 10;

    /// <summary>
    /// 把 <paramref name="id"/> 放到 <paramref name="ordered"/>（顯示順序：已排序的在前、未排序的在後）第 <paramref name="index"/> 個
    /// （不算它自己）所需的寫入；位置沒變時為空。
    /// <list type="bullet">
    /// <item>落在已排序的範圍內：取前後值的中間；沒有空間時把已排序的整段重新編號。</item>
    /// <item>落在未排序者之間或之後：它前面的未排序者依序接在最後一個值之後，一起變成已排序，畫面上的順序就是存下來的順序。</item>
    /// </list>
    /// </summary>
    public static IReadOnlyList<(TKey Id, int SortOrder)> Plan<TKey>(IReadOnlyList<(TKey Id, int? SortOrder)> ordered, TKey id, int index, int max)
        where TKey : notnull
    {
        var same = EqualityComparer<TKey>.Default;
        var from = -1;
        for (var i = 0; i < ordered.Count && from < 0; i++) if (same.Equals(ordered[i].Id, id)) from = i;
        var list = ordered.Where(x => !same.Equals(x.Id, id)).ToList();
        var at = Math.Clamp(index, 0, list.Count);
        // 原地放下不寫入：畫面沒變，未排序的人也不因此變成已排序。
        if (from == at) return [];
        list.Insert(at, from >= 0 ? ordered[from] : (id, null));
        var firstUnsorted = list.Take(at).ToList().FindIndex(x => x.SortOrder is null);
        if (firstUnsorted >= 0)
        {
            var start = firstUnsorted > 0 ? list[firstUnsorted - 1].SortOrder!.Value : 0;
            var tail = list.GetRange(firstUnsorted, at - firstUnsorted + 1);
            return (long)start + tail.Count * Step <= max
                ? Changed(tail.Select((x, i) => (x.Id, start + (i + 1) * Step)), list, id)
                : Renumber(list.GetRange(0, at + 1), max, id);
        }
        var before = at > 0 ? list[at - 1].SortOrder : null;
        var after = at + 1 < list.Count ? list[at + 1].SortOrder : null;
        return Between(before, after, max) is { } value
            ? [(id, value)]
            : Renumber([.. list.Where((x, i) => i <= at || x.SortOrder is not null)], max, id);
    }

    private static IReadOnlyList<(TKey Id, int SortOrder)> Renumber<TKey>(IReadOnlyList<(TKey Id, int? SortOrder)> items, int max, TKey id) where TKey : notnull
    {
        var step = Math.Max(1, Math.Min(Step, max / Math.Max(1, items.Count)));
        return Changed(items.Select((x, i) => (x.Id, (i + 1) * step)), items, id);
    }

    /// <summary>只留值有變的與被移動的那一筆。</summary>
    private static IReadOnlyList<(TKey Id, int SortOrder)> Changed<TKey>(IEnumerable<(TKey Id, int SortOrder)> writes, IReadOnlyList<(TKey Id, int? SortOrder)> items, TKey id) where TKey : notnull
    {
        var same = EqualityComparer<TKey>.Default;
        var values = items.ToDictionary(x => x.Id, x => x.SortOrder);
        return [.. writes.Where(x => same.Equals(x.Id, id) || values[x.Id] != x.SortOrder)];
    }

    /// <summary>前後值之間可用的值；<paramref name="after"/> 為 null 代表後面沒有已排序的值。沒有空間時回傳 null。</summary>
    private static int? Between(int? before, int? after, int max)
    {
        var value = (before, after) switch
        {
            (null, null) => Step,
            (null, { } next) => Math.Max(0, next - Step),
            ({ } previous, null) => Math.Min(max, previous + Step),
            ({ } previous, { } next) => (int)(((long)previous + next) / 2),
        };
        return (before is null || value > before) && (after is null || value < after) ? value : null;
    }
}
