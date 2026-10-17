using AiNexus.Platform.Errors;

namespace AiNexus.Features.AccessControl;

/// <summary>所有模組共用的存取控制錯誤。</summary>
public static class AccessControlErrors
{
    /// <summary>
    /// 帳號缺少該操作所需的功能授權。所有功能共用這一個代碼，不論是 policy 擋下請求，或 handler、交易、背景工作重新檢查授權。
    /// 回應不寫出是哪個功能（一個 policy 可能接受多個功能之一）；需要該功能的頁面本身已知道是哪一個。
    /// </summary>
    public static readonly Error FeatureForbidden = Error.Forbidden("feature_forbidden");
}
