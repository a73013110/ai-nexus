using AiNexus.Features.AccessControl;
using AiNexus.Features.Inference;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Administration;

/// <summary>
/// Platform-wide accounts and model providers for administrators. It describes the whole platform, so it is left out
/// when a single user is selected.
/// </summary>
internal sealed class PlatformDashboard(NexusDbContext db, ModelCatalog catalog, TimeProvider clock) : IDashboardSource
{
    public string Feature => FeatureIds.Admin;

    public bool PlatformOnly => true;

    public async Task<DashboardSectionDto?> ReadAsync(DashboardScope scope, CancellationToken ct)
    {
        if (scope.Owner is not null) return null;
        var now = clock.GetUtcNow();
        var users = await db.Users.AsNoTracking().Where(x => x.DeletedAt == null).GroupBy(x => 1).Select(g => new
        {
            Enabled = g.Count(x => x.Enabled),
            Disabled = g.Count(x => !x.Enabled),
            Locked = g.Count(x => x.LockedUntil > now),
            Seen = g.Count(x => x.Enabled && x.LastSeenAt >= scope.From && x.LastSeenAt < scope.Until),
        }).SingleOrDefaultAsync(ct);
        var calling = await db.Set<ModelInvocation>().AsNoTracking().Where(x => x.CreatedAt >= scope.From && x.CreatedAt < scope.Until)
            .Select(x => x.OwnerId).Distinct().CountAsync(ct);
        var audit = await DashboardDays.CountAsync(db, db.AuditEvents.AsNoTracking().Select(x => x.At), scope, ct);
        var locked = users?.Locked ?? 0;
        var section = new DashboardSection("platform", Feature, "平台")
            .Count("users", "啟用帳號", users?.Enabled ?? 0, "人", hint: users?.Disabled > 0 ? $"另有 {users.Disabled} 個停用" : null)
            .Period("seen", "期間登入", users?.Seen ?? 0, "人", hint: "依最後上線時間")
            .Period("calling", "期間使用 AI", calling, "人", hint: "期間有模型或工具呼叫的帳號")
            .Count("locked", "暫時鎖定", locked, "人", locked > 0 ? DashboardTones.Warning : DashboardTones.Neutral, "登入失敗過多")
            .Period("audit", "稽核事件", audit.Sum(x => x.Value), "筆")
            .Daily("audit", "每日稽核事件", "筆", audit)
            .Share("seen", "期間登入", users?.Seen ?? 0, DashboardTones.Success)
            .Share("idle", "期間未登入", Math.Max(0, (users?.Enabled ?? 0) - (users?.Seen ?? 0)));
        foreach (var provider in await catalog.ProviderStatusesAsync(ct))
            section.Fact(provider.Id switch { "google" => "Google AI", "ollama" => "地端 Ollama", _ => provider.Id },
                provider.Available ? "可連線" : "無法連線", provider.Available ? DashboardTones.Success : DashboardTones.Danger);
        if (section.Build().Facts.Any(x => x.Tone == DashboardTones.Danger)) section.State(DashboardStates.Attention);
        return section.Build();
    }
}
