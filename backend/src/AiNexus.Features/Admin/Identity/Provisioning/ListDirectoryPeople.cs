using AiNexus.Features.Admin.Identity.Authentication;
using AiNexus.Platform.Errors;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Identity.Provisioning;

/// <summary>節點人員；<c>More</c> 表示超過 <see cref="ListDirectoryPeople.Limit"/> 人，只回依姓名排序的前段，畫面請管理員改選下層節點。</summary>
public sealed record DirectoryPeopleDto(IReadOnlyList<DirectoryPersonDto> People, bool More);

/// <summary>
/// 匯入對話框選取節點後列出的人員（<c>identity.users.import</c>）：直屬或含子節點，附平台對應狀態與以 AD 更新時會改的欄位。
/// 一次回完整清單（上限 1000 人），篩選、搜尋與全選都在畫面上做：LDAP 的分頁游標綁在連線上，無法跨請求翻頁。
/// </summary>
internal sealed class ListDirectoryPeople(IAdDirectory directory, DirectoryProvisioning provisioning, IOptions<AdAuthenticationOptions> options)
{
    public const int Limit = 1000;

    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/directory-people", (string? node, bool? subtree, ListDirectoryPeople handler, CancellationToken ct) => handler.HandleAsync(node, subtree == true, ct).ToHttpResultAsync())
        .RequireRateLimiting(IdentityModule.DirectoryReadRateLimit)
        .WithName("ListDirectoryPeople");

    public async Task<Result<DirectoryPeopleDto>> HandleAsync(string? node, bool subtree, CancellationToken ct)
    {
        if (ListDirectoryNodes.Resolve(options.Value, node) is not { } dn) return IdentityErrors.DirectoryNodeInvalid;
        if (await directory.PeopleAsync(dn, subtree, Limit, ct) is not { } listing) return IdentityErrors.DirectoryNodeNotFound;
        return new DirectoryPeopleDto(await provisioning.DescribeAsync(listing.People, ct), listing.More);
    }
}
