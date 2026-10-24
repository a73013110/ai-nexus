using AiNexus.Data.App;
using AiNexus.Data.App.Jobs;
using AiNexus.Features.Admin.Identity.Management;
using AiNexus.Platform.Events;

namespace AiNexus.Features.Work.Jobs;

/// <summary>
/// 帳號停用時取消他排隊中與執行中的背景工作（ADR 0019）。直接取消，不依賴各處理器在執行時重查權限：
/// 不是每個處理器都會在每一步重查，停用後的工作只是佔著 worker。復職後本人可以重試。
/// </summary>
internal sealed class CancelDisabledOwnerJobs(AppDbContext db, TimeProvider clock) : IDomainEventHandler<UserDisabled>
{
    public Task HandleAsync(UserDisabled e, CancellationToken ct) => db.Set<BackgroundJob>().OwnedBy(e.UserId).RequestCancelAsync(clock.GetUtcNow(), ct);
}
