using AiNexus.Data.App;
using AiNexus.Data.App.Flow;
using AiNexus.Data.App.Repositories;
using AiNexus.Features.Office.Flow;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Repositories.Access;

/// <summary>
/// 申請種類 <c>repository-access</c>（ADR 0037）：事實 <c>permission</c> 給關卡條件用（例如只有寫入要主管審）。
/// 核准時在簽核的交易內寫入授權；儲存庫已停止開放申請、已封存或已不在 Gitea 時拒絕核准，審核人看到原因後可以退回。
/// </summary>
internal sealed class RepositoryAccessFlow(AppDbContext db, RepositoryAccess access, TimeProvider clock) : IFlowKind
{
    public const string Kind = "repository-access";
    public const string PermissionFact = "permission";

    public static readonly FlowKindDefinition Definition = new(Kind, "儲存庫權限", "申請 Gitea 儲存庫的讀取或寫入權限；核准後自動加入協作者。")
    {
        Facts = [new(PermissionFact, "申請的權限", [new(RepositoryPermissions.Read, "讀取"), new(RepositoryPermissions.Write, "寫入")])],
    };

    public async Task<Result> CompleteAsync(FlowCompletion completion, CancellationToken ct)
    {
        if (completion.Status != FlowStatuses.Approved) return Result.Success;
        var request = await db.RepositoryAccessRequests.AsNoTracking().SingleAsync(x => x.Id == completion.SubjectId, ct);
        if (!await db.ManagedRepositories.AnyAsync(x => x.Id == request.RepositoryId && x.Requestable && !x.IsArchived && x.MissingSince == null, ct))
            return RepositoriesErrors.NotRequestable;
        if (request.ExpiresAt <= clock.GetUtcNow()) return RepositoriesErrors.ExpiryInvalid;

        // 審核期間已經拿到同等以上、且不會更早到期的權限（例如負責人直接給了）：保留現有授權，不因核准而降級或縮短。
        var current = await access.Active(request.RepositoryId, request.UserId, null).AsNoTracking().SingleOrDefaultAsync(ct);
        if (current is not null && RepositoryAccess.Covers(current, request.Permission, request.ExpiresAt)) return Result.Success;
        await access.GrantAsync(new(request.RepositoryId, request.UserId, null, request.Permission, request.ExpiresAt, RepositoryGrantSources.Request)
            { RequestId = request.Id }, completion.ActorId, ct);
        return Result.Success;
    }
}
