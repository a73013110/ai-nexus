using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.AccessControl.Navigation;

/// <summary>
/// 新增選單節點。<c>ParentId</c> 為 null 代表最上層；<c>OpenInNewTab</c> 只對 link 有意義，其他種類一律存成預設值。
/// <c>AudienceGroupIds</c> 只能給 link，null 或空清單＝所有登入者可見。
/// </summary>
public sealed record NavigationItemCreateRequest(string Kind, string? ParentId, string? FeatureId, string? Name, string? Url, bool OpenInNewTab, string? Icon,
    IReadOnlyList<string>? AudienceGroupIds) : INavigationItemContent;

internal sealed class NavigationItemCreateRequestValidator : RequestValidator<NavigationItemCreateRequest>
{
    public NavigationItemCreateRequestValidator()
    {
        Include(new NavigationItemContentValidator());
        RuleFor(x => x.ParentId).MaximumLength(NavigationItem.IdMaxLength);
    }
}

/// <summary>新增分群、頁面或外部連結，排到同層最後。Id 由伺服器產生（內建節點的可讀 Id 只來自 Seed）。會稽核。</summary>
internal sealed class CreateNavigationItem(AppDbContext db, AdministrativeWrite write, IOptions<AccessControlOptions> settings)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPost("/navigation-items", (NavigationItemCreateRequest body, CreateNavigationItem handler, CancellationToken ct) =>
            handler.HandleAsync(body, ct))
        .Audited(AccessControlAudit.NavigationCreated)
        .WithName("CreateNavigationItem");

    public async Task<Result<NavigationItemDto>> HandleAsync(NavigationItemCreateRequest request, CancellationToken ct)
    {
        var id = Guid.NewGuid().ToString("N");
        var audiences = request.AudienceGroupIds ?? [];
        var begun = await write.BeginAsync(NavigationRules.Target(db, id), ct);
        if (!begun.IsSuccess) return begun.Error;
        await using var transaction = begun.Value;

        if (await NavigationRules.ContentAsync(db, request, settings.Value.NavigationLinkHosts, ct) is { } content) return content;
        var items = await db.NavigationItems.ToListAsync(ct);
        var last = items.Where(x => x.ParentId == request.ParentId).Select(x => (int?)x.SortOrder).Max();
        var item = new NavigationItem
        {
            Id = id,
            ParentId = request.ParentId,
            Kind = request.Kind,
            // 排到最後；同層已排到上限時與最後一個同值，再依 Id 排，不因此拒絕新增。
            SortOrder = Math.Min((last ?? 0) + NavigationRules.SortStep, NavigationRules.MaxSortOrder),
        };
        NavigationRules.Apply(item, request);
        if (NavigationRules.Structure([.. items, item]) is { } invalid) return invalid;
        db.Add(item);
        await NavigationRules.ReplaceAudiencesAsync(db, id, audiences, ct);

        var committed = await transaction.CommitAsync(ct);
        return committed.IsSuccess ? NavigationItemDto.From(item, [.. audiences.Order(StringComparer.Ordinal)]) : committed.Error;
    }
}
