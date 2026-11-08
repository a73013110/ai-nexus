using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Scheduling.Management;

/// <summary>暫停與恢復共用的寫入對象：稽核 key 是任務 Key，快照只有是否暫停。</summary>
internal static class ScheduledTaskRules
{
    public static AuditTarget Target(AppDbContext db, string key) => AuditTarget.Of("scheduling:" + key, async ct => await db.ScheduledTasks.AsNoTracking()
        .Where(x => x.Key == key).Select(x => new { paused = x.IsPaused }).SingleOrDefaultAsync(ct));
}
