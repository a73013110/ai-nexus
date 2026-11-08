namespace AiNexus.Platform.Data;

/// <summary>
/// 大量 DELETE／UPDATE 分批執行（保存期清理、過期標記）。每批是一句自動提交的語句，行鎖數遠低於 SQL Server 的鎖升級門檻（約 5,000），
/// 不會升級成資料表鎖阻塞正常讀寫，也不開長交易；中途被取消時已提交的批次保留，下次從資料狀態接續。呼叫端不要包交易。
/// </summary>
public static class Batches
{
    /// <summary>每批筆數；查詢以 <c>Take(Batches.Size)</c> 限制。</summary>
    public const int Size = 1000;

    /// <summary>重複執行 <paramref name="batch"/>，直到某一批少於 <see cref="Size"/> 筆，回傳總筆數。</summary>
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
