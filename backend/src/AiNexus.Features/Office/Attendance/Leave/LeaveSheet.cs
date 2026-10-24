namespace AiNexus.Features.Office.Attendance.Leave;

/// <summary>公文系統的單據種類。數值寫在 <see cref="LeaveSheetReader"/> 的 SQL 裡。</summary>
public enum LeaveSheetKind
{
    Leave = 0,
    Travel = 1
}

/// <summary>
/// 公文系統（唯讀）單據的一段連續期間。<paramref name="Title"/> 是請假單的假別名稱（查不到假別時退回假別代碼），出差單則是事由；時間是 +08:00。
/// 跨區間假單（<c>Form_Leave_Group</c>）的每一段各自一筆、<paramref name="DesNo"/> 相同：出勤逐日判定看的是實際請假的時段，
/// 一般單據也可能跨日，呼叫端本來就要依日切開；合併成一張單帶多段期間，只是多一層要攤平。要算張數時以 <paramref name="DesNo"/> 去重。
/// </summary>
public sealed record LeaveSheet(string DesNo, LeaveSheetKind Kind, string Title, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string FlowStatus);
