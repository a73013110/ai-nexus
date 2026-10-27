using AiNexus.Features.Admin.Identity.Authentication;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.Identity.Provisioning;

/// <summary>目錄節點與其中的人數；<c>People</c> 超過計數上限時為 null（畫面不顯示人數）。</summary>
public sealed record DirectoryNodeDto(string Dn, string Name, string Kind, int? People, bool HasChildren);

/// <summary>
/// <c>Node</c> 是查詢的節點本身（未指定時是搜尋根），<c>Children</c> 是它的直屬子節點（依名稱排序）。
/// <c>Writable</c> 是服務帳號能否寫回 AD（見 <see cref="IAdDirectory.Writable"/>）；<c>MoreChildren</c> 表示子節點超過上限，只列前面一段。
/// </summary>
public sealed record DirectoryNodesDto(DirectoryNodeDto Node, IReadOnlyList<DirectoryNodeDto> Children, bool Writable, bool MoreChildren);

/// <summary>
/// 匯入對話框的目錄樹（<c>identity.users.import</c>）：一次展開一層。只能瀏覽設定的搜尋根之下，DN 由前一次回應帶回，
/// 不接受任意 DN 以免把服務帳號當成整個樹系的瀏覽器。目錄在公司內本來就可讀，所以只限流、不寫稽核。
/// </summary>
internal sealed class ListDirectoryNodes(IAdDirectory directory, IOptions<AdAuthenticationOptions> options)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/directory-nodes", (string? parent, ListDirectoryNodes handler, CancellationToken ct) => handler.HandleAsync(parent, ct).ToHttpResultAsync())
        .RequireRateLimiting(IdentityModule.DirectoryReadRateLimit)
        .Audited(IdentityAudit.DirectoryRead)
        .WithName("ListDirectoryNodes");

    public async Task<Result<DirectoryNodesDto>> HandleAsync(string? parent, CancellationToken ct)
    {
        if (Resolve(options.Value, parent) is not { } dn) return IdentityErrors.DirectoryNodeInvalid;
        if (await directory.ChildrenAsync(dn, ct) is not { } branch) return IdentityErrors.DirectoryNodeNotFound;
        var settings = options.Value;
        var root = dn.Equals(settings.Base, StringComparison.OrdinalIgnoreCase);
        var node = new DirectoryNodeDto(dn, root && settings.SearchBase.Length == 0 ? settings.Domain : DirectoryNames.NameOf(dn),
            root ? DirectoryNodeKinds.Root : dn.StartsWith("OU=", StringComparison.OrdinalIgnoreCase) ? DirectoryNodeKinds.OrganizationalUnit : DirectoryNodeKinds.Container,
            branch.People, branch.Children.Count > 0);
        return new DirectoryNodesDto(node, [.. branch.Children.Select(x => new DirectoryNodeDto(x.Dn, x.Name, x.Kind, x.People, x.HasChildren))], directory.Writable,
            branch.MoreChildren);
    }

    /// <summary>
    /// 要查詢的 DN：未指定時是搜尋根；指定時必須是搜尋根或在它之下，長度與字元在合理範圍內（DN 只當作搜尋根傳給 LDAP，不組進篩選條件）。
    /// </summary>
    public static string? Resolve(AdAuthenticationOptions settings, string? node)
    {
        if (string.IsNullOrWhiteSpace(node)) return settings.Base;
        var dn = node.Trim();
        return dn.Length <= 1024 && !dn.Any(char.IsControl) && DirectoryNames.Within(dn, settings.Base) ? dn : null;
    }
}
