using AiNexus.Data.App;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.AccessControl.Navigation;

/// <summary>
/// 修改節點內容。<c>Kind</c> 必須等於節點現有的種類（種類不能改），放在 body 是為了讓 validator 不查資料就能檢查欄位組合；
/// 位置與排序改用 <see cref="ReorderNavigationItems"/>。
/// </summary>
public sealed record NavigationItemUpdateRequest(string Kind, string? FeatureId, string? Name, string? Url, bool OpenInNewTab, string? Icon,
    IReadOnlyList<string>? AudienceGroupIds, long RowVersion) : INavigationItemContent;

internal sealed class NavigationItemUpdateRequestValidator : RequestValidator<NavigationItemUpdateRequest>
{
    public NavigationItemUpdateRequestValidator()
    {
        Include(new NavigationItemContentValidator());
        RuleFor(x => x.RowVersion).GreaterThan(0).WithErrorCode(RuleCode.Required);
    }
}

/// <summary>修改分群、頁面或外部連結的內容（不改種類、不改位置）。會稽核。</summary>
internal sealed class UpdateNavigationItem(AppDbContext db, AdministrativeWrite write, IOptions<AccessControlOptions> settings)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPut("/navigation-items/{id}", (string id, NavigationItemUpdateRequest body, UpdateNavigationItem handler, CancellationToken ct) =>
            handler.HandleAsync(id, body, ct))
        .Audited(AccessControlAudit.NavigationUpdated)
        .WithName("UpdateNavigationItem");

    public async Task<Result> HandleAsync(string id, NavigationItemUpdateRequest request, CancellationToken ct)
    {
        var begun = await write.BeginAsync(NavigationRules.Target(db, id), ct);
        if (!begun.IsSuccess) return begun.Error;
        await using var transaction = begun.Value;

        var items = await db.NavigationItems.ToListAsync(ct);
        var item = items.SingleOrDefault(x => x.Id == id);
        if (item is null) return AccessControlErrors.NavigationItemNotFound;
        if (item.Kind != request.Kind) return AccessControlErrors.NavigationKindMismatch;
        if (await NavigationRules.ContentAsync(db, request, settings.Value.NavigationLinkHosts, ct) is { } content) return content;
        db.ExpectRowVersion(item, request.RowVersion);
        NavigationRules.Apply(item, request);
        // 換成另一個功能可能與同層既有的頁面重複。
        if (NavigationRules.Structure(items) is { } invalid) return invalid;
        await NavigationRules.ReplaceAudiencesAsync(db, id, request.AudienceGroupIds ?? [], ct);

        return await transaction.CommitAsync(ct);
    }
}
