using System.Collections;
using System.Collections.Immutable;

namespace AiNexus.Generators.Common;

/// <summary>
/// 依內容比較的唯讀陣列。incremental pipeline 以相等性判斷上一步的輸出是否改變，<see cref="ImmutableArray{T}"/> 只比參考，
/// 放進模型會讓每次按鍵都重新產生；模型裡的集合一律用這個型別。
/// </summary>
internal readonly struct EquatableArray<T>(ImmutableArray<T> items) : IEquatable<EquatableArray<T>>, IEnumerable<T> where T : IEquatable<T>
{
    private readonly ImmutableArray<T> items = items.IsDefault ? [] : items;

    public int Count => Items.Length;

    public bool Equals(EquatableArray<T> other) => Items.AsSpan().SequenceEqual(other.Items.AsSpan());

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        var hash = 17;
        foreach (var item in Items) hash = unchecked((hash * 31) + item.GetHashCode());
        return hash;
    }

    public ImmutableArray<T>.Enumerator GetEnumerator() => Items.GetEnumerator();

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => ((IEnumerable<T>)Items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)Items).GetEnumerator();

    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right) => left.Equals(right);

    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right) => !left.Equals(right);

    // default(EquatableArray<T>) 的欄位是 default 陣列；一律視為空，比較與列舉才不會丟例外。
    private ImmutableArray<T> Items => items.IsDefault ? [] : items;
}
