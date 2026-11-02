using Microsoft.EntityFrameworkCore;

namespace AiNexus.Data.App.Identity;

/// <summary>帳號的並行檢查，見 <see cref="User.ProfileVersion"/>。</summary>
public static class UserProfileVersion
{
    /// <summary>要求 <paramref name="user"/> 的版本還是 <paramref name="version"/>（前端讀到的值）並遞增：不符時 SaveChanges 拋 <see cref="DbUpdateConcurrencyException"/>。遞增一定會送 UPDATE，只改子表（角色）也會比對。</summary>
    public static void ExpectProfileVersion(this DbContext db, User user, int version)
    {
        var property = db.Entry(user).Property(x => x.ProfileVersion);
        property.OriginalValue = version;
        property.CurrentValue = version + 1;
    }
}
