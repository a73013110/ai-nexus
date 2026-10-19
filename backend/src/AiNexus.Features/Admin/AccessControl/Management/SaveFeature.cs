using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Features.Admin.AccessControl.Navigation;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.AccessControl.Management;

/// <summary>
/// <c>Name</c> 空白或等於目錄預設名稱時清除覆寫（之後跟著目錄改名）；<c>GroupId</c> 為 null 代表放在選單頂層；
/// <c>SortOrder</c> 決定它在該分群內的順序。網址與圖示由程式目錄決定，不能在這裡改。
/// </summary>
[ValidatedInHandler("Format failures are audited inside the administrative transaction (AccessRules).")]
public sealed record FeatureUpdateRequest(string? Name, int SortOrder, bool Enabled, string? GroupId);

/// <summary>
/// 更名、調整排序、換群組、啟用或停用既有功能。會稽核；若會移除執行者自己的功能管理權限則拒絕。
/// </summary>
internal sealed class SaveFeature(AppDbContext db, AdministrativeAudit audit)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPut("/features/{id}", async (string id, FeatureUpdateRequest body, IRequestActor actor, SaveFeature handler, CancellationToken ct) => (await handler.HandleAsync(actor.Id, id, body, ct)).ToHttpResult())
        .WithName("SaveFeature");

    public Task<Result> HandleAsync(Guid actor, string id, FeatureUpdateRequest request, CancellationToken ct) => audit.MutateAsync(actor, FeatureCatalog.AccessControlFeatures, "admin.feature", null, id, token => SnapshotAsync(id, token), async () =>
    {
        // 不檢查 id 格式：功能 id 由目錄定義，資料庫也可能有目錄不認得的舊列；查無此 id 就直接回 NotFound。
        var name = string.IsNullOrWhiteSpace(request.Name) ? null : request.Name.Trim();
        if (((name is null ? null : AccessRules.Name(name)) ?? NavigationRules.Order(request.SortOrder)) is { } invalid) return invalid;
        var feature = await db.Set<Feature>().FindAsync([id], ct);
        if (feature is null) return AccessControlErrors.AdminResourceNotFound;
        if (await NavigationRules.ExistingGroupAsync(db, request.GroupId, ct) is { } missing) return missing;
        // 與預設相同就不存覆寫，目錄日後改預設名稱時這一列會跟著變。目錄不認得的功能沒有預設，照存。
        feature.Name = name == FeatureCatalog.Find(id)?.Name ? null : name;
        feature.SortOrder = request.SortOrder; feature.Enabled = request.Enabled; feature.GroupId = request.GroupId;
        return Result.Success;
    }, ct);

    private async Task<object?> SnapshotAsync(string id, CancellationToken ct)
    {
        var feature = await db.Set<Feature>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return feature is null ? null : new { feature.Name, feature.Enabled, feature.SortOrder, feature.GroupId };
    }
}
