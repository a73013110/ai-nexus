using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Accounts;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Identity.Reviews;

/// <summary>帳號待辦的一項。<c>Route</c> 是處理頁的站內網址；沒有處理頁或檢視者沒有該頁的功能時為 null。</summary>
public sealed record AccountReviewTaskDto(string Code, string Title, string? Detail, string? Route);

/// <summary>有待辦的一位使用者；項目依來源註冊順序。</summary>
public sealed record AccountReviewUserDto(Guid Id, string Account, string DisplayName, bool Enabled, bool Deleted, IReadOnlyList<AccountReviewTaskDto> Items);

/// <summary>帳號待辦：有待辦的使用者（依顯示名稱），以及這次沒能完整檢查的說明。</summary>
public sealed record AccountReviewsDto(IReadOnlyList<AccountReviewUserDto> Users, IReadOnlyList<string> Notices);

/// <summary>
/// 彙整各模組的 <see cref="IAccountReviewSource"/>（ADR 0019）。待辦每次重新查詢，不保存「已處理」：
/// 處理完條件就不成立，項目自然消失，不會有待辦與實際資料不同步的問題。
/// </summary>
internal sealed class ListAccountReviews(AppDbContext db, AccessService access, IEnumerable<IAccountReviewSource> sources)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/account-reviews", async (ICurrentUser user, ListAccountReviews handler, CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(user.Id, ct)))
        .WithName("ListAccountReviews");

    public async Task<AccountReviewsDto> HandleAsync(Guid actor, CancellationToken ct)
    {
        var items = new List<AccountReviewItem>();
        var notices = new List<string>();
        foreach (var source in sources)
        {
            var reading = await source.ReadAsync(ct);
            items.AddRange(reading.Items);
            if (reading.Notice is { } notice) notices.Add(notice);
        }

        var routes = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var feature in items.Select(x => x.Feature).OfType<string>().Distinct(StringComparer.Ordinal))
            routes[feature] = await access.HoldsAsync(actor, feature, ct) ? FeatureCatalog.RouteOf(feature) : null;

        var ids = items.Select(x => x.UserId).Distinct().ToArray();
        var users = await db.Users.AsNoTracking().Where(x => ids.Contains(x.Id))
            .Select(x => new { x.Id, x.Account, x.DisplayName, x.Enabled, Deleted = x.DeletedAt != null }).ToDictionaryAsync(x => x.Id, ct);
        var grouped = items.Where(x => users.ContainsKey(x.UserId)).GroupBy(x => x.UserId).Select(group =>
        {
            var user = users[group.Key];
            return new AccountReviewUserDto(user.Id, user.Account, user.DisplayName, user.Enabled && !user.Deleted, user.Deleted,
                [.. group.Select(x => new AccountReviewTaskDto(x.Code, x.Title, x.Detail, Route(x)))]);
        });
        return new([.. grouped.OrderBy(x => x.DisplayName, StringComparer.CurrentCulture).ThenBy(x => x.Id)], notices);

        string? Route(AccountReviewItem item)
            => item.Feature is { } feature && routes[feature] is { } route ? item.Query is { } query ? route + "?" + query : route : null;
    }
}
