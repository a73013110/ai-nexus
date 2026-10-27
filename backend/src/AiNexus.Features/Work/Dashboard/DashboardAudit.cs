using AiNexus.Platform.Auditing;

namespace AiNexus.Features.Work.Dashboard;

/// <summary>總覽的稽核動作（ADR 0023）。名稱是稽核合約，寫入後不可改。</summary>
public static class DashboardAudit
{
    public static readonly AuditAction OverviewRead = AuditAction.Access("dashboard.overview.read", "檢視總覽");
}
