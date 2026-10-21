using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;

namespace AiNexus.Features.Admin.Identity.Users;

/// <summary>已啟用且未刪除的使用者 id 查詢，供其他模組透過 <see cref="IActiveUsers"/> 取得可用使用者集合。</summary>
internal sealed class ActiveUsers(AppDbContext db) : IActiveUsers
{
    // accesscontrol.udf_UserGroups 在 SQL 寫了同樣的條件（儲存上限的群組來源），改這裡要一起改。
    public IQueryable<Guid> Ids => db.Users.Where(x => x.Enabled && x.DeletedAt == null).Select(x => x.Id);
}
