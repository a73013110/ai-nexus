using AiNexus.Data.App;
using AiNexus.Data.App.Attendance;

namespace AiNexus.Features.Office.Attendance.Punches;

/// <summary>
/// 一筆原始刷卡與它的歸戶結果，卡片詳情與匯入明細共用。<see cref="Holder"/> 是當時持有人的帳號顯示名稱，沒有帳號時是留下的姓名；
/// <see cref="Unattributed"/> 表示沒有持有期間涵蓋，不會出現在出勤紀錄。機器與事件代號原樣保存、不解讀。
/// 歸戶在讀取時依目前的持有期間計算，所以同一筆刷卡的持有人會隨 HR 調整期間而變。
/// </summary>
public sealed record PunchDto(long Id, string CardNumber, DateTimeOffset PunchedAt, string MachineCode, string EventCode, bool Unattributed, Guid? UserId, string? Holder);

internal static class PunchQueries
{
    /// <summary>投影保留來源的排序；持有人有帳號時取帳號的顯示名稱（改名後跟著變），否則用持有期間留下的姓名。</summary>
    public static IQueryable<PunchDto> ToDtos(this IQueryable<AttributedPunch> punches, AppDbContext db)
        => from p in punches
           join u in db.Users on p.UserId equals u.Id into users
           from u in users.DefaultIfEmpty()
           select new PunchDto(p.PunchId, p.CardNumber, p.PunchedAt, p.MachineCode, p.EventCode, p.IsUnattributed, p.UserId, u != null ? u.DisplayName : p.HolderName);
}
