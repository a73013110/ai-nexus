using AiNexus.Data.App;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.AccessControl.Management;

/// <summary>
/// <c>Name</c> 空白或等於目錄預設名稱時清除覆寫（之後跟著目錄改名）。網址與圖示由程式目錄決定、選單位置在選單頁（NavigationItems），都不能在這裡改。
/// </summary>
[ValidatedInHandler("Format failures return the specific codes of AccessRules, checked in the handler before the transaction.")]
public sealed record FeatureUpdateRequest(string? Name, bool Enabled, long RowVersion);

/// <summary>
/// 更名、啟用或停用既有功能。會稽核；若會移除執行者自己的功能管理權限則拒絕。
/// </summary>
internal sealed class SaveFeature(AppDbContext db, AdministrativeWrite write)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPut("/features/{id}", (string id, FeatureUpdateRequest body, IRequestActor actor, SaveFeature handler, CancellationToken ct) =>
            handler.HandleAsync(actor.Id, id, body, ct))
        .Audited(AccessControlAudit.FeatureUpdated)
        .WithName("SaveFeature");

    public async Task<Result> HandleAsync(Guid actor, string id, FeatureUpdateRequest request, CancellationToken ct)
    {
        // 不檢查 id 格式：功能 id 由目錄定義，資料庫也可能有目錄不認得的舊列；查無此 id 就直接回 NotFound。
        var name = request.Name.TrimToNull();
        if (name is not null && AccessRules.Name(name) is { } invalid) return invalid;

        var begun = await write.BeginAsync(AuditTarget.Of(id, token => SnapshotAsync(id, token)).KeepingAccess(actor, FeatureCatalog.AccessControlFeatures), ct);
        if (!begun.IsSuccess) return begun.Error;
        await using var transaction = begun.Value;

        var feature = await db.Features.FindAsync([id], ct);
        if (feature is null) return AccessControlErrors.AdminResourceNotFound;
        db.ExpectRowVersion(feature, request.RowVersion);
        // 與預設相同就不存覆寫，目錄日後改預設名稱時這一列會跟著變。目錄不認得的功能沒有預設，照存。
        feature.Name = name == FeatureCatalog.Find(id)?.Name ? null : name;
        feature.Enabled = request.Enabled;

        return await transaction.CommitAsync(ct);
    }

    private async Task<object?> SnapshotAsync(string id, CancellationToken ct)
    {
        var feature = await db.Features.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return feature is null ? null : new { feature.Name, feature.Enabled };
    }
}
