using System.Globalization;
using AiNexus.Platform.Data.Sql;
using Dapper;

namespace AiNexus.Platform.Dashboard;

/// <summary>
/// 總覽來源的資料讀取：每個來源一支 procedure（例如 <c>jobs.usp_GetJobsDashboard</c>），一次往返取回區塊需要的全部結果集。
/// 參數固定為 <c>@Owner</c>（NULL＝全平台）、<c>@Actor</c>（項目清單只列他自己的）、<c>@From</c>、<c>@Until</c>、<c>@OffsetMinutes</c>，
/// 每日分桶由 <c>dbo.udf_DashboardDay</c> 統一切日；授權與呈現（文字、色調、顯示名稱）留在呼叫端。
/// </summary>
public static class DashboardProcedure
{
    /// <summary>以固定參數呼叫 <paramref name="procedure"/>；<paramref name="arguments"/> 是來源自己的額外參數（例如由 <see cref="TimeProvider"/> 算出的現在時間），名稱不可與固定參數重複。</summary>
    public static Task<TResult> QueryDashboardAsync<TDatabase, TResult>(this ISqlDatabase<TDatabase> sql, string procedure, DashboardScope scope,
        Func<DashboardResults, Task<TResult>> read, CancellationToken ct, object? arguments = null)
    {
        var parameters = new DynamicParameters(new { scope.Owner, scope.Actor, scope.From, scope.Until, scope.OffsetMinutes });
        if (arguments is not null) parameters.AddDynamicParams(arguments);
        return sql.QueryProcedureAsync(procedure, parameters, results => read(new DashboardResults(results)), cancellationToken: ct);
    }
}

/// <summary>
/// 總覽 procedure 的結果集，依序為：計數（一列）→ 每日 <c>(Day, Value)</c> → 分佈 <c>([Key], Value)</c> → 最近項目。
/// 沒有分佈或項目的來源省略該結果集，但不改變其餘的順序；每個結果集只能讀一次。
/// </summary>
public sealed class DashboardResults(SqlResults results)
{
    /// <summary>計數列；欄位名稱、順序與型別要和 <typeparamref name="T"/> 的建構式一致（COUNT 是 INT、位元組是 BIGINT、平均是 FLOAT）。</summary>
    public Task<T> CountsAsync<T>() => results.ReadSingleAsync<T>();

    /// <summary>期間內有資料的曆日，依日期排序；沒有資料的日子不出現，前端視為 0。</summary>
    public async Task<IReadOnlyList<DashboardPointDto>> DaysAsync()
        => [.. (await results.ReadAsync<DayRow>()).Select(x => new DashboardPointDto(x.Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), x.Value))];

    /// <summary>分佈的原始數量；標籤、色調與「其他」的合併由呼叫端決定。</summary>
    public Task<IReadOnlyList<DashboardShareRow>> SharesAsync() => results.ReadAsync<DashboardShareRow>();

    public Task<IReadOnlyList<T>> ItemsAsync<T>() => results.ReadAsync<T>();

    private sealed record DayRow(DateTime Day, int Value);
}

/// <summary>分佈結果集的一列：分類鍵（模型、種類、狀態…）與數量。</summary>
public sealed record DashboardShareRow(string Key, int Value);
