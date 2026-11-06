using System.Text.Json;

namespace AiNexus.Features.Admin.Operations;

/// <summary>使用者列表的篩選條件；分頁（<c>offset</c>）另外帶。條件之間是「且」，由 <c>operations.usp_ListAdminUsers</c> 套用。</summary>
public sealed class AdminUserFilter
{
    /// <summary>一次最多篩幾個角色：畫面上是從角色清單多選，上限只擋異常的網址。</summary>
    public const int MaxRoles = 50;
    public const int SearchMaxLength = 120;

    /// <summary>比對姓名、AD 帳號、本地帳號與通知信箱的任一部分。</summary>
    public string? Search { get; set; }
    /// <summary>帳號是否啟用；不帶＝全部。</summary>
    public bool? Enabled { get; set; }
    /// <summary>角色 ID（<c>?role=a&amp;role=b</c>）；擁有其中任一角色即符合。</summary>
    public string[]? Role { get; set; }

    internal bool IsValid()
        => !(Search?.Length > SearchMaxLength) && !(Role?.Length > MaxRoles)
           && Role?.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > AiNexus.Data.App.AccessControl.Role.IdMaxLength) != true;

    /// <summary>角色條件的 JSON 陣列（procedure 以 OPENJSON 展開）；沒有角色條件時為 null。</summary>
    internal string? RoleIdsJson() => Role is { Length: > 0 } roles ? JsonSerializer.Serialize(roles.Distinct(StringComparer.Ordinal)) : null;
}
