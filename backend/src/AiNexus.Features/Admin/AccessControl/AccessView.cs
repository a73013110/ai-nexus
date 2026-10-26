using AiNexus.Features.Admin.AccessControl.Navigation;
using AiNexus.Features.Admin.AccessControl.Scopes;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Admin.AccessControl;

/// <summary>
/// 繪製畫面用的授權：在 <see cref="AccessService.ForUserAsync"/> 的結果上加入選單樹（<see cref="NavigationTree.Build"/>）與各功能的資料範圍代碼，
/// 供 <c>/me</c> 與存取預覽使用；授權檢查直接呼叫 <see cref="AccessService.ForUserAsync"/>，略過這些查詢。
/// 外部連結每次都再比對允許清單：主機移出清單（設定改完、站台重新啟動）後，已存的連結就不再輸出。
/// </summary>
public sealed class AccessView(AccessService access, IDataScope scopes, IOptions<AccessControlOptions> settings)
{
    public async Task<AccessDto> ForUserAsync(Guid user, CancellationToken ct)
    {
        var grants = await access.ForUserAsync(user, ct);
        var tree = NavigationTree.Build(await access.NavigationItemsAsync(ct), grants.Features, grants.Groups.Select(x => x.Id), settings.Value.NavigationLinkHosts);
        return grants with { Navigation = tree, DataScopes = await scopes.ScopesAsync(user, ct) };
    }
}
