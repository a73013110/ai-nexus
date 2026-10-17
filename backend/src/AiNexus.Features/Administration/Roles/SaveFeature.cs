using AiNexus.Features.AccessControl;
using AiNexus.Features.Administration.Navigation;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;

namespace AiNexus.Features.Administration.Roles;

/// <summary><c>GroupId</c> 為 null 代表放在選單頂層；<c>SortOrder</c> 決定它在該分群內的順序。</summary>
[ValidatedInHandler("Format failures are audited inside the administrative transaction (AccessRules).")]
public sealed record FeatureUpdateRequest(string Name, int SortOrder, bool Enabled, string? GroupId, string Icon);

/// <summary>
/// 更名、調整排序、換群組、換圖示、啟用或停用既有功能。會稽核；若會移除執行者自己的管理員權限則拒絕。
/// </summary>
internal sealed class SaveFeature(NexusDbContext db, AdministrativeAudit audit)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPut("/features/{id}", async (string id, FeatureUpdateRequest body, SaveFeature handler, CancellationToken ct) => (await handler.HandleAsync(id, body, ct)).ToHttpResult())
        .WithName("SaveFeature");

    public Task<Result> HandleAsync(string id, FeatureUpdateRequest request, CancellationToken ct) => audit.MutateAsync("admin.feature", null, id, async () =>
    {
        // 不檢查 id 格式：功能 id 由各模組定義（如 `logs.query`），查無此 id 就直接回 NotFound。
        if ((AccessRules.Name(request.Name) ?? NavigationRules.Order(request.SortOrder) ?? NavigationRules.Icon(request.Icon)) is { } invalid) return invalid;
        var feature = await db.Set<Feature>().FindAsync([id], ct);
        if (feature is null) return AdministrationErrors.NotFound;
        if (await NavigationRules.ExistingGroupAsync(db, request.GroupId, ct) is { } missing) return missing;
        feature.Name = request.Name.Trim(); feature.SortOrder = request.SortOrder; feature.Enabled = request.Enabled;
        feature.GroupId = request.GroupId; feature.Icon = request.Icon;
        return Result.Success;
    }, ct);
}
