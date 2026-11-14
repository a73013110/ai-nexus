using System.Globalization;
using AiNexus.Data.App;
using AiNexus.Data.App.Repositories;
using AiNexus.Features.Admin.Identity;
using AiNexus.Features.Admin.Identity.Users;
using AiNexus.Features.Office.Flow;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Work.Repositories.Access;

/// <summary>申請的權限只能是讀取或寫入（管理權限只由管理員授予）；到期日含當天，留空為不限。</summary>
public sealed record RepositoryAccessRequestBody(string Permission, DateOnly? ExpiresOn, string Reason);

/// <summary>送出後的簽核申請；沒有適用關卡時已直接核准。</summary>
public sealed record RepositoryAccessRequestedDto(Guid InstanceId);

internal sealed class RepositoryAccessRequestBodyValidator : RequestValidator<RepositoryAccessRequestBody>
{
    public RepositoryAccessRequestBodyValidator()
    {
        RuleFor(x => x.Permission).Must(x => x is RepositoryPermissions.Read or RepositoryPermissions.Write).WithErrorCode(RuleCode.Invalid);
        RuleFor(x => x.Reason).RequiredContent(RepositoryAccessRequest.ReasonMaxLength);
    }
}

/// <summary>
/// 申請儲存庫權限：寫入申請內容並送進簽核（種類 <c>repository-access</c>），核准時由 <see cref="RepositoryAccessFlow"/> 寫入授權。
/// 送出前確認 Gitea 有申請人的帳號，避免核准後才發現加不進去。會稽核。
/// </summary>
internal sealed class RequestRepositoryAccess(
    AppDbContext db, FlowService flow, IGiteaServiceClient gitea, AuditTrail audit, IOptions<GiteaOptions> options, TimeProvider clock) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapPost("/{id:guid}/requests", (Guid id, RepositoryAccessRequestBody body, ICurrentUser user, RequestRepositoryAccess handler, CancellationToken ct) =>
            handler.HandleAsync(id, user.Id, body, ct))
        .Audited(RepositoriesAudit.AccessRequested)
        .WithName("RequestRepositoryAccess");

    public async Task<Result<RepositoryAccessRequestedDto>> HandleAsync(Guid id, Guid actor, RepositoryAccessRequestBody body, CancellationToken ct)
    {
        if (!options.Value.AccessEnabled) return RepositoriesErrors.AccessDisabled;
        var repository = await db.ManagedRepositories.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (repository is null) return RepositoriesErrors.ManagedNotFound;
        if (!repository.Requestable || repository.IsArchived || repository.MissingSince is not null) return RepositoriesErrors.NotRequestable;
        var expiresAt = body.ExpiresOn is { } date ? RepositoryAccess.ExpiresAt(date) : (DateTimeOffset?)null;
        if (expiresAt <= clock.GetUtcNow()) return RepositoriesErrors.ExpiryInvalid;
        // Gitea 的檢查在交易外：等外部服務時不佔住使用者列的鎖。
        var login = await db.Users.Where(x => x.Id == actor).Select(x => x.AdAccount).SingleOrDefaultAsync(ct);
        if (login is null || !await gitea.UserExistsAsync(login, ct)) return RepositoriesErrors.AccountMissing;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.LockUserRowAsync(actor, ct);
        var current = await db.RepositoryGrants.AsNoTracking().SingleOrDefaultAsync(x => x.RepositoryId == id && x.UserId == actor && x.EndedAt == null, ct);
        if (current is not null && RepositoryAccess.Covers(current, body.Permission, expiresAt)) return RepositoriesErrors.AccessHeld;
        if (await flow.PendingSubjects(RepositoryAccessFlow.Kind).AnyAsync(s => db.RepositoryAccessRequests.Any(r => r.Id == s && r.RepositoryId == id && r.UserId == actor), ct))
            return RepositoriesErrors.AccessPending;

        var request = new RepositoryAccessRequest { RepositoryId = id, UserId = actor, Permission = body.Permission, ExpiresAt = expiresAt, Reason = body.Reason.Trim() };
        db.RepositoryAccessRequests.Add(request);
        // 先寫入申請：沒有適用關卡時簽核在送出當下就核准，核准會讀這筆申請。
        await db.SaveChangesAsync(ct);
        var permission = RepositoryAccess.PermissionName(body.Permission);
        var submitted = await flow.SubmitAsync(new(RepositoryAccessFlow.Kind, request.Id, actor, $"{repository.FullName} {permission}權限")
        {
            Facts = new Dictionary<string, string> { [RepositoryAccessFlow.PermissionFact] = body.Permission },
            Details =
            [
                new("儲存庫", repository.FullName), new("權限", permission),
                new("到期日", body.ExpiresOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "不限"), new("理由", request.Reason),
            ],
        }, ct);
        if (!submitted.IsSuccess) return submitted.Error;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        audit.Describe(id, actor, new { permission = body.Permission, expiresAt, instanceId = submitted.Value });
        return new RepositoryAccessRequestedDto(submitted.Value);
    }
}
