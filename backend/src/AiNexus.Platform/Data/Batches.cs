using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace AiNexus.Platform.Data;

/// <summary>
/// 大量 DELETE／UPDATE 分批執行（保存期清理、過期標記）。每批是一句自動提交的語句，行鎖數遠低於 SQL Server 的鎖升級門檻（約 5,000），
/// 不會升級成資料表鎖阻塞正常讀寫，也不開長交易；中途被取消時已提交的批次保留，下次從資料狀態接續。呼叫端不要包交易。
/// </summary>
public static class Batches
{
    /// <summary>每批筆數。</summary>
    public const int Size = 1000;

    /// <summary>分批刪除 <paramref name="rows"/> 的每一筆，回傳總筆數。要由舊到新刪就先 <c>OrderBy</c>（走得到索引時才有意義）。</summary>
    public static Task<int> DeleteInBatchesAsync<T>(this IQueryable<T> rows, CancellationToken ct) where T : class
        => RepeatAsync(() => rows.Take(Size).ExecuteDeleteAsync(ct));

    /// <summary>
    /// 分批更新 <paramref name="rows"/> 的每一筆，回傳總筆數。更新後的列必須不再符合 <paramref name="rows"/> 的條件
    /// （例如條件是「未讀」、更新是「標為已讀」），否則每批都取到同一批列、不會結束。
    /// </summary>
    public static Task<int> UpdateInBatchesAsync<T>(this IQueryable<T> rows, Action<UpdateSettersBuilder<T>> setters, CancellationToken ct) where T : class
        => RepeatAsync(() => rows.Take(Size).ExecuteUpdateAsync(setters, ct));

    /// <summary>
    /// 重複執行 <paramref name="batch"/>，直到某一批少於 <see cref="Size"/> 筆，回傳總筆數。
    /// 只給組不成單一 <see cref="IQueryable{T}"/> 的情況用（例如依設定換資料表），<paramref name="batch"/> 自己要以 <see cref="Size"/> 限制筆數；其餘用上面兩個方法。
    /// </summary>
    public static async Task<int> RepeatAsync(Func<Task<int>> batch)
    {
        var total = 0;
        int affected;
        do
        {
            affected = await batch();
            total += affected;
        }
        while (affected >= Size);
        return total;
    }
}
