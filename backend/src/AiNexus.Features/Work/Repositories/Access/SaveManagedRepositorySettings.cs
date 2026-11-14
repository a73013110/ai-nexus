using AiNexus.Data.App;
using AiNexus.Data.App.Repositories;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Repositories.Access;

/// <summary>是否開放申請與負責人名單（整份覆寫）；負責人必須是在職人員。</summary>
public sealed record ManagedRepositorySettingsRequest(bool Requestable, IReadOnlyList<Guid> StewardIds, long RowVersion);

internal sealed class ManagedRepositorySettingsRequestValidator : RequestValidator<ManagedRepositorySettingsRequest>
{
    public const int MaxStewards = 20;

    public ManagedRepositorySettingsRequestValidator()
    {
        RuleFor(x => x.StewardIds).NotNull().WithErrorCode(RuleCode.Required)
            .Must(x => x is null || x.Count <= MaxStewards).WithErrorCode(RuleCode.TooMany)
            .Must(x => x is null || x.Distinct().Count() == x.Count).WithErrorCode(RuleCode.Duplicate);
        RuleFor(x => x.RowVersion).GreaterThan(0).WithErrorCode(RuleCode.Required);
    }
}

/// <summary>管理員設定一個儲存庫：是否開放申請、負責人。只影響之後的申請與審核；審核中的申請下次重算時換成新的負責人。會稽核前後值。</summary>
internal sealed class SaveManagedRepositorySettings(AppDbContext db, AdministrativeWrite write, IActiveUsers activeUsers) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapPut("/{id:guid}/settings", (Guid id, ManagedRepositorySettingsRequest body, SaveManagedRepositorySettings handler, CancellationToken ct) =>
            handler.HandleAsync(id, body, ct))
        .RequireFeature(FeatureCatalog.RepositoriesAccessManage)
        .Audited(RepositoriesAudit.ManagedRepositoryUpdated)
        .WithName("SaveManagedRepositorySettings");

    public async Task<Result> HandleAsync(Guid id, ManagedRepositorySettingsRequest request, CancellationToken ct)
    {
        var active = activeUsers.Ids;
        if (request.StewardIds.Any(x => !active.Contains(x))) return RepositoriesErrors.StewardInvalid;

        var begun = await write.BeginAsync(AuditTarget.Of(id, token => SnapshotAsync(db, id, token)), ct);
        if (!begun.IsSuccess) return begun.Error;
        await using var transaction = begun.Value;
        var repository = await db.ManagedRepositories.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (repository is null) return RepositoriesErrors.ManagedNotFound;
        db.ExpectRowVersion(repository, request.RowVersion);
        repository.Requestable = request.Requestable;
        // 只改負責人時也要更新這一列，版本才會前進：兩位管理員同時改名單時後送出的才會收到衝突。
        db.Entry(repository).Property(x => x.Requestable).IsModified = true;

        var current = await db.RepositoryStewards.Where(x => x.RepositoryId == id).ToListAsync(ct);
        db.RepositoryStewards.RemoveRange(current.Where(x => !request.StewardIds.Contains(x.UserId)));
        db.RepositoryStewards.AddRange(request.StewardIds.Except(current.Select(x => x.UserId)).Select(x => new RepositorySteward { RepositoryId = id, UserId = x }));
        return await transaction.CommitAsync(ct);
    }

    private static async Task<object?> SnapshotAsync(AppDbContext db, Guid id, CancellationToken ct) => new
    {
        requestable = await db.ManagedRepositories.Where(x => x.Id == id).Select(x => (bool?)x.Requestable).SingleOrDefaultAsync(ct),
        stewardIds = await db.RepositoryStewards.Where(x => x.RepositoryId == id).OrderBy(x => x.UserId).Select(x => x.UserId).ToListAsync(ct),
    };
}
