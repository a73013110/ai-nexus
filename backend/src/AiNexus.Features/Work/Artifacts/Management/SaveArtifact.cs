using AiNexus.Data.App;
using AiNexus.Data.App.Artifacts;
using AiNexus.Features.Work.Collaboration;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Artifacts.Management;

/// <summary>編輯時帶入所依據的 <c>ExpectedVersion</c>；若資料庫已有更新的版本，即為衝突。</summary>
public sealed record SaveArtifactRequest(string Title, string Content, int ExpectedVersion);

/// <summary>只驗證內容；標題不合法時由 handler 先回傳自己的錯誤代碼。</summary>
internal sealed class SaveArtifactRequestValidator : RequestValidator<SaveArtifactRequest>
{
    public override string ProblemCode => ArtifactsErrors.ContentInvalidCode;

    public SaveArtifactRequestValidator()
    {
        When(x => Artifact.TitleIsValid(x.Title), () => RuleFor(x => x.Content).Must(Artifact.ContentIsValid).WithErrorCode("invalid"));
    }
}

/// <summary>對使用者可編輯的成果新增一筆不可變的版本；不會覆寫其他人在此期間保存的版本。</summary>
internal sealed class SaveArtifact(
    AppDbContext db,
    ResourceAccess access,
    ResourceWriteLock writes,
    GetArtifact reader,
    TimeProvider clock, AuditTrail audit)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPut("/{id:guid}", async (
                Guid id, SaveArtifactRequest request, ICurrentUser user, SaveArtifact handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, id, request, ct)).ToHttpResult())
        .Audited(ArtifactsAudit.ArtifactUpdated)
        .WithName("SaveArtifact");

    public async Task<Result<ArtifactDto>> HandleAsync(Guid actor, Guid id, SaveArtifactRequest request, CancellationToken ct)
    {
        if (!Artifact.TitleIsValid(request.Title)) return CollaborationErrors.InvalidName;
        if (request.ExpectedVersion is < 1 or >= Artifact.MaxVersions) return ArtifactsErrors.VersionLimit;
        using (await writes.AcquireAsync(id, ct))
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var found = await access.RequireAsync(actor, id, Artifact.Kind, ct, write: true);
            if (!found.IsSuccess) return found.Error;
            var resource = found.Value;
            // 版本號以條件式 UPDATE 遞增：只有讀到的版本仍是最新時才會成功，否則即為併發衝突。
            var changed = await db.Set<Artifact>()
                .Where(x => x.Id == id && x.Version == request.ExpectedVersion)
                .ExecuteUpdateAsync(p => p.SetProperty(x => x.Version, x => x.Version + 1), ct);
            if (changed != 1) return ArtifactsErrors.VersionConflict;
            var now = clock.GetUtcNow();
            var newVersion = request.ExpectedVersion + 1;
            resource.Name = request.Title.Trim();
            resource.UpdatedAt = now;
            db.Add(new ArtifactRevision
            {
                ArtifactId = id,
                Version = newVersion,
                AuthorId = actor,
                Title = resource.Name,
                Content = request.Content,
                CreatedAt = now,
            });
            audit.Describe(details: new { version = newVersion });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return await reader.HandleAsync(actor, id, null, ct);
        }
    }
}
