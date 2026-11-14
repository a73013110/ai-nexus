using AiNexus.Data.App;
using AiNexus.Data.App.Repositories;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Work.Repositories.Access;

/// <summary>
/// 直接授予：人員給 <paramref name="UserId"/>；部署等非人員帳號給 <paramref name="Login"/>（只有管理員，且要寫理由）。
/// 同一人已有的授權會被這筆取代，所以改權限、改到期日也用這個。
/// </summary>
public sealed record RepositoryGrantRequest(Guid? UserId, string? Login, string Permission, DateOnly? ExpiresOn, string? Reason);

internal sealed class RepositoryGrantRequestValidator : RequestValidator<RepositoryGrantRequest>
{
    public RepositoryGrantRequestValidator()
    {
        RuleFor(x => x.Login).OptionalText(RepositoryGrant.LoginMaxLength)
            .Must((x, login) => x.UserId is null != string.IsNullOrWhiteSpace(login)).WithErrorCode(RuleCode.Invalid);
        RuleFor(x => x.Permission).Must(x => RepositoryPermissions.Rank(x) > 0).WithErrorCode(RuleCode.Invalid);
        RuleFor(x => x.Reason).OptionalContent(RepositoryGrant.ReasonMaxLength);
    }
}

/// <summary>負責人或管理員直接授予（ADR 0037）：負責人只能給在職人員讀取或寫入，管理權限與非人員帳號只有管理員能給。會稽核。</summary>
internal sealed class GrantRepositoryAccess(
    AppDbContext db, RepositoryAccess access, IGiteaServiceClient gitea, IActiveUsers activeUsers, AuditTrail audit, IOptions<GiteaOptions> options,
    TimeProvider clock) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapPost("/{id:guid}/grants", (Guid id, RepositoryGrantRequest body, ICurrentUser user, GrantRepositoryAccess handler, CancellationToken ct) =>
            handler.HandleAsync(id, user.Id, body, ct))
        .Audited(RepositoriesAudit.GrantGranted)
        .WithName("GrantRepositoryAccess");

    public async Task<Result> HandleAsync(Guid id, Guid actor, RepositoryGrantRequest body, CancellationToken ct)
    {
        if (!options.Value.AccessEnabled) return RepositoriesErrors.AccessDisabled;
        var manager = await access.ManagerAsync(actor, id, ct);
        if (manager == RepositoryManager.None) return RepositoriesErrors.AccessForbidden;
        if (manager == RepositoryManager.Steward && (body.UserId is null || body.Permission == RepositoryPermissions.Admin)) return RepositoriesErrors.AccessForbidden;
        if (!await db.ManagedRepositories.AnyAsync(x => x.Id == id && x.MissingSince == null, ct)) return RepositoriesErrors.ManagedNotFound;
        var expiresAt = body.ExpiresOn is { } date ? RepositoryAccess.ExpiresAt(date) : (DateTimeOffset?)null;
        if (expiresAt <= clock.GetUtcNow()) return RepositoriesErrors.ExpiryInvalid;
        var reason = body.Reason.TrimToNull();

        string login;
        if (body.UserId is { } user)
        {
            var account = await db.Users.Where(x => x.Id == user).Select(x => x.AdAccount).SingleOrDefaultAsync(ct);
            if (!activeUsers.Ids.Contains(user) || account is null) return RepositoriesErrors.GranteeInvalid;
            login = account;
        }
        else
        {
            // 對得上人員的登入名要授權給那個人：同一個 Gitea 帳號只能有一筆期望狀態，人員離職時才查得到。
            login = body.Login!.Trim();
            var account = login;
            if (reason is null || await db.Users.AnyAsync(x => x.AdAccount == account, ct)) return RepositoriesErrors.GranteeInvalid;
        }
        if (!await gitea.UserExistsAsync(login, ct)) return RepositoriesErrors.AccountMissing;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var source = manager == RepositoryManager.Administrator ? RepositoryGrantSources.Admin : RepositoryGrantSources.Steward;
        await access.GrantAsync(new(id, body.UserId, body.UserId is null ? login : null, body.Permission, expiresAt, source)
            { GrantedById = actor, Reason = reason }, actor, ct);
        try { await db.SaveChangesAsync(ct); }
        // 人員的授權在 GrantAsync 內排隊；非人員帳號同時授予兩次時由唯一索引擋下。
        catch (DbUpdateException error) when (AdministrativeWrite.IsWriteConflict(error)) { return RepositoriesErrors.AccessChanged; }
        await transaction.CommitAsync(ct);
        audit.Describe(id, body.UserId, new { account = login, permission = body.Permission, expiresAt, source, reason });
        return Result.Success;
    }
}
