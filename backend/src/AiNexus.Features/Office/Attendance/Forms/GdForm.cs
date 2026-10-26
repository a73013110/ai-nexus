using AiNexus.Features.Platform.Gd;

namespace AiNexus.Features.Office.Attendance.Forms;

/// <summary>公文系統的表單種類。數值寫在 <see cref="GdFormReader"/> 的 SQL 裡。</summary>
public enum GdFormKind
{
    Leave = 0,
    Travel = 1
}

/// <summary>
/// 公文系統（唯讀）表單的一段連續期間。<paramref name="Title"/> 是請假單的假別名稱（查不到假別時退回假別代碼），出差單則是事由；時間是 +08:00。
/// 跨區間假單（<c>Form_Leave_Group</c>）的每一段各自一筆、<paramref name="DesNo"/> 相同：出勤逐日判定看的是實際請假的時段，
/// 一般表單也可能跨日，呼叫端本來就要依日切開；合併成一張單帶多段期間，只是多一層要攤平。要算張數時以 <paramref name="DesNo"/> 去重。
/// <paramref name="CurrentSigner"/>、<paramref name="CurrentStep"/> 是流程中（<see cref="GdFormFlowOver.Send"/>）的單目前停在誰、哪一關（例如「請假代理人」），
/// 取自 GD <c>FLOW_CURRENT</c>；其他狀態或查不到時為 null。
/// </summary>
public sealed record GdForm(string DesNo, GdFormKind Kind, string Title, DateTimeOffset StartsAt, DateTimeOffset EndsAt, GdFormFlowOver FlowStatus,
    string? CurrentSigner = null, string? CurrentStep = null)
{
    /// <summary>已核准，才算補足遲到、早退與缺勤；流程中的單只並列。</summary>
    public bool Approved => FlowStatus == GdFormFlowOver.Decide;
}
