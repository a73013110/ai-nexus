using AiNexus.Data.App;
using AiNexus.Data.App.Releases;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Releases;

/// <summary>
/// 程式需要的最低資料庫版本與目標庫記錄的版本比對（ADR 0028）。只顯示、不擋啟動：精確的檢查是啟動時的 EF 結構比對。
/// </summary>
internal static class ReleaseCompatibility
{
    public const string Compatible = "compatible", Incompatible = "incompatible", Unchecked = "unchecked";

    /// <summary>
    /// 程式沒有最低版本（未打包、還沒有資料庫發版）或任一邊無法解析時為 <see cref="Unchecked"/>；目標庫沒有版本紀錄而程式有要求時為
    /// <see cref="Incompatible"/>：那是 update.sql 從未執行過的庫。
    /// </summary>
    public static string Of(string? minimum, string? database)
    {
        if (ReleaseVersion.Parse(minimum) is not { } required) return Unchecked;
        if (database is null) return Incompatible;
        return ReleaseVersion.Parse(database) is { } actual ? actual >= required ? Compatible : Incompatible : Unchecked;
    }

    /// <summary>該部分（<see cref="DeploymentComponents"/>）最近一次的部署，即目前版本；走 <c>(Component, Id)</c> 索引。</summary>
    public static Task<Deployment?> LatestAsync(AppDbContext db, string component, CancellationToken ct)
        => db.Set<Deployment>().AsNoTracking().Where(x => x.Component == component).OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
}
