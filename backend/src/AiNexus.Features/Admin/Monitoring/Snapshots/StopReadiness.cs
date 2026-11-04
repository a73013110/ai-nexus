using AiNexus.Platform.Monitoring;

namespace AiNexus.Features.Admin.Monitoring.Snapshots;

/// <summary>
/// 把各子系統的 <see cref="StopImpact"/> 加總成「現在能不能停站」，是純函式。
/// 有會中斷的工作就直接回答 <see cref="Interrupts"/>；否則只要有子系統讀不到（尚未讀取或讀取失敗）就不能斷言可以停站，
/// 回答 <see cref="Unknown"/>，寧可讓維運多等一次更新，也不在看不到的時候說「可以停」。
/// </summary>
public static class StopReadiness
{
    public const string Clear = "clear", Interrupts = "interrupts", Unknown = "unknown";

    /// <summary>尚未執行 <see cref="Evaluate"/> 前的佔位值。</summary>
    public static readonly StopSummary Pending = new(Unknown, 0, 0);

    public static StopSummary Evaluate(MonitoringSnapshot snapshot)
    {
        if (!snapshot.Enabled) return Pending;
        int interrupts = 0, resumes = 0;
        var blind = false;
        foreach (var subsystem in snapshot.Subsystems)
        {
            interrupts += subsystem.Stop?.Interrupts ?? 0;
            resumes += subsystem.Stop?.Resumes ?? 0;
            blind |= subsystem.Status == SubsystemStates.Unobserved;
        }
        var status = interrupts > 0 ? Interrupts : blind || snapshot.Subsystems.Length == 0 ? Unknown : Clear;
        return new(status, interrupts, resumes);
    }
}
