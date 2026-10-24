using System.Linq.Expressions;
using AiNexus.Data.App;
using AiNexus.Data.App.Identity;
using AiNexus.Features.Admin.AccessControl;

namespace AiNexus.Features.Admin.Identity.Users;

/// <summary>已啟用且未刪除的使用者 id 查詢，供其他模組透過 <see cref="IActiveUsers"/> 取得可用使用者集合。</summary>
internal sealed class ActiveUsers(AppDbContext db) : IActiveUsers
{
    /// <summary>
    /// 平台認定的「在職」：啟用且未刪除（ADR 0019，停用＝離職）。Identity 內的查詢用這個運算式、記憶體判斷用 <see cref="IsActive"/>，
    /// 其他模組用 <see cref="IActiveUsers"/>，定義只有這一處。accesscontrol.udf_UserGroups 在 SQL 寫了同樣的條件（儲存上限的群組來源），改這裡要一起改。
    /// </summary>
    public static readonly Expression<Func<User, bool>> Active = x => x.Enabled && x.DeletedAt == null;

    private static readonly Func<User, bool> Compiled = Active.Compile();

    public static bool IsActive(User user) => Compiled(user);

    public IQueryable<Guid> Ids => db.Users.Where(Active).Select(x => x.Id);
}
