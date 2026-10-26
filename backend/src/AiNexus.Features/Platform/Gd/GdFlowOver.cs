namespace AiNexus.Features.Platform.Gd;

/// <summary>
/// 表單（Form：請假單、出差單，加班單同一套）的流程狀態，對應 <c>FlowOver</c> 的字串代碼；成員名就是代碼，JSON 也照原字串輸出。
/// GD 資料庫沒有對照表，字樣與語意取自 GD 程式（<c>FormUtil.FlowStatic</c>，寫入處見各成員）。
/// </summary>
public enum GdFormFlowOver
{
    /// <summary>暫存：還沒送出。</summary>
    Temp,

    /// <summary>流程中：送出後還在簽核，目前關卡在 <c>FLOW_CURRENT</c>。</summary>
    Send,

    /// <summary>退回：退給申請人，可修改後再送出（同一單號回到 <see cref="Send"/>）。</summary>
    Back,

    /// <summary>核准：最後一關決行（<c>LeaveFormBean</c>、<c>TravelFormMode</c> 寫入）。只有這個狀態代表表單有效。</summary>
    Decide,

    /// <summary>銷單：核准後撤銷。</summary>
    Cancel,

    /// <summary>
    /// 退回結案：承辦人在「暫存與退回」清單把退回的單結案存查（<c>FormTempBean.BackFinish</c>，<c>JudgeUserId</c> 寫承辦人）。
    /// 名稱像「完成」但不是核准；舊系統的出缺勤計算曾把它當有效表單。
    /// </summary>
    Finish
}

/// <summary>簽呈（Frm：外來文、函、簽等）的流程狀態，對應數字代碼（GD <c>FormUtil.FlowOverStatic</c>）。目前沒有讀取簽呈，先定義好對照。</summary>
public enum GdFrmFlowOver
{
    Temp = 0,
    Send = 1,
    Decide = 2,
    Back = 3,
    DecideFinish = 4,
    BackFinish = 5
}

/// <summary>GD 流程狀態的中文字樣（與 GD 畫面相同）與解析。</summary>
public static class GdFlowOver
{
    public static string Label(GdFormFlowOver flow) => flow switch
    {
        GdFormFlowOver.Temp => "暫存",
        GdFormFlowOver.Send => "流程中",
        GdFormFlowOver.Back => "退回",
        GdFormFlowOver.Decide => "核准",
        GdFormFlowOver.Cancel => "銷單",
        GdFormFlowOver.Finish => "退回結案",
        _ => flow.ToString()
    };

    public static string Label(GdFrmFlowOver flow) => flow switch
    {
        GdFrmFlowOver.Temp => "暫存",
        GdFrmFlowOver.Send => "流程中",
        GdFrmFlowOver.Decide => "核准",
        GdFrmFlowOver.Back => "退回",
        GdFrmFlowOver.DecideFinish => "核准結案",
        GdFrmFlowOver.BackFinish => "退回結案",
        _ => flow.ToString()
    };

    /// <summary>
    /// 解析 <c>FlowOver</c> 字串：GD 是 char 欄位比對（尾端空白、大小寫不分），這裡同樣忽略；數字或其他字串不接受，
    /// 免得 <see cref="Enum.TryParse{TEnum}(string, bool, out TEnum)"/> 把 <c>"3"</c> 當成第四個成員。
    /// </summary>
    public static bool TryParse(string? code, out GdFormFlowOver flow)
    {
        flow = default;
        var text = code?.Trim();
        return !string.IsNullOrEmpty(text) && char.IsLetter(text[0]) && Enum.TryParse(text, ignoreCase: true, out flow) && Enum.IsDefined(flow);
    }
}
