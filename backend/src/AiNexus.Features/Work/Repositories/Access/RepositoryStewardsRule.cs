using AiNexus.Data.App;
using AiNexus.Features.Office.Flow;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Repositories.Access;

/// <summary>審核人規則 <c>repository-stewards</c>：申請的儲存庫目前的負責人任一人審核；沒有負責人時引擎交給簽核管理員。只給 <c>repository-access</c> 用。</summary>
internal sealed class RepositoryStewardsRule(AppDbContext db) : IApproverRule
{
    public static readonly ApproverRuleDefinition Definition = new("repository-stewards", "儲存庫負責人",
        "申請的儲存庫負責人，任一人審核。\n負責人由系統管理員在儲存庫權限頁指定。", ApproverArguments.None) { Kinds = [RepositoryAccessFlow.Kind] };

    public Task<Error?> ValidateAsync(string? argument, CancellationToken ct) => Task.FromResult<Error?>(null);

    public async Task<IReadOnlyCollection<Guid>> ResolveAsync(ApproverContext context, CancellationToken ct)
        => await db.RepositoryStewards.AsNoTracking()
            .Where(s => db.RepositoryAccessRequests.Any(r => r.Id == context.SubjectId && r.RepositoryId == s.RepositoryId))
            .Select(s => s.UserId).ToListAsync(ct);
}
