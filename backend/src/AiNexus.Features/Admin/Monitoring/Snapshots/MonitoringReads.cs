using AiNexus.Features.Admin.Monitoring.Traffic;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Admin.Monitoring.Snapshots;

/// <summary>管理員讀取即時監控的入口：只接受 <see cref="RuntimeTraffic"/> 保留的時間窗；稽核由端點宣告的請求稽核寫。</summary>
internal static class MonitoringReads
{
    /// <summary>可選的時間窗（分鐘），由短到長；<see cref="MonitoringSnapshots"/> 依此順序各快取一份。最長不可超過流量保留時間。</summary>
    public static readonly int[] Windows = [1, 5, 15];
    /// <summary>未指定 <c>minutes</c> 時的時間窗。</summary>
    public const int DefaultWindow = 5;

    public static bool ValidWindow(int minutes) => Windows.Contains(minutes);

    /// <summary>單次讀取（檢視或匯出）：驗證時間窗並取共用快照。</summary>
    public static Result<MonitoringSnapshot> Read(MonitoringSnapshots snapshots, int window)
        => ValidWindow(window) ? snapshots.Read(window) : MonitoringErrors.InvalidWindow;
}
