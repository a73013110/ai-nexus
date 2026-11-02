using AiNexus.Data.App;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Identity.Users;

/// <summary>每人一份的規則（上限、一人一筆）用的資料庫鎖：同一個使用者的請求排隊，不同人互不等待，跨程序也有效。</summary>
public static class UserRowLock
{
    /// <summary>
    /// 以值不變的 UPDATE 鎖住使用者列，鎖隨交易釋放，所以要在交易內、讀取要保護的資料之前呼叫（RCSI 的查詢開始時才取快照，鎖之後讀得到對方剛提交的內容）。
    /// 要同時取應用程式鎖時先取應用程式鎖（<see cref="AiNexus.Platform.Data.ApplicationLocks"/>）。
    /// </summary>
    public static Task<int> LockUserRowAsync(this AppDbContext db, Guid user, CancellationToken ct) => db.Users.Where(x => x.Id == user)
        .ExecuteUpdateAsync(p => p.SetProperty(x => x.LastSeenAt, x => x.LastSeenAt), ct);
}
