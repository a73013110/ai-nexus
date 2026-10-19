using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.AccessControl.Navigation;

/// <summary>一個節點的新位置：<c>ParentId</c> 為 null 代表最上層。</summary>
public sealed record NavigationOrderItem(string Id, string? ParentId, int SortOrder);

/// <summary>批次移動與排序；只列出要變更的節點，沒列到的維持原位。</summary>
public sealed record NavigationOrderRequest(IReadOnlyList<NavigationOrderItem> Items);

internal sealed class NavigationOrderRequestValidator : RequestValidator<NavigationOrderRequest>
{
    public override string ProblemCode => AccessControlErrors.InvalidOrder.Code;

    public NavigationOrderRequestValidator()
    {
        RuleFor(x => x.Items).NotEmpty().Must(x => x.Count <= 1000).WithErrorCode("too_many")
            .Must(x => x.Select(y => y.Id).Distinct(StringComparer.Ordinal).Count() == x.Count).WithErrorCode("duplicate");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.Id).NotEmpty().MaximumLength(NavigationItem.IdMaxLength);
            item.RuleFor(x => x.ParentId).MaximumLength(NavigationItem.IdMaxLength);
            item.RuleFor(x => x.SortOrder).InclusiveBetween(0, NavigationRules.MaxSortOrder);
        });
    }
}

/// <summary>
/// 拖曳或鍵盤排序後一次送出的移動與排序。套到完整的節點清單後才檢查結構（父節點、成環、深度、同層重複），
/// 全部通過才寫入；同一個交易，不會留下移了一半的樹。會稽核。
/// </summary>
internal sealed class ReorderNavigationItems(AppDbContext db, AdministrativeAudit audit)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPut("/navigation-items/order", async (NavigationOrderRequest body, IRequestActor actor, ReorderNavigationItems handler, CancellationToken ct) => (await handler.HandleAsync(actor.Id, body, ct)).ToHttpResult())
        .WithName("ReorderNavigationItems");

    public Task<Result> HandleAsync(Guid actor, NavigationOrderRequest request, CancellationToken ct)
    {
        var ids = request.Items.Select(x => x.Id).ToArray();
        return audit.MutateAsync(actor, FeatureCatalog.AccessControlNavigation, "admin.navigation_order", null, "order", token => SnapshotAsync(ids, token), async () =>
        {
            var items = await db.Set<NavigationItem>().ToListAsync(ct);
            var byId = items.ToDictionary(x => x.Id, StringComparer.Ordinal);
            foreach (var move in request.Items)
            {
                if (!byId.TryGetValue(move.Id, out var item)) return AccessControlErrors.NavigationItemNotFound;
                item.ParentId = move.ParentId; item.SortOrder = move.SortOrder;
            }
            // 失敗時 AdministrativeAudit 不會 SaveChanges，並在寫失敗稽核前清空 ChangeTracker，上面改到一半的實體不會寫入。
            return NavigationRules.Structure(items) is { } invalid ? invalid : Result.Success;
        }, ct);
    }

    private async Task<object?> SnapshotAsync(IReadOnlyList<string> ids, CancellationToken ct)
        => await db.Set<NavigationItem>().AsNoTracking().Where(x => ids.Contains(x.Id)).OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.ParentId, x.SortOrder }).ToArrayAsync(ct);
}
