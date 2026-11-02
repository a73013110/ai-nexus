using AiNexus.Data.App;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.Support;

/// <summary>
/// 讀資料列目前的版本，給只想驗證其他規則的測試帶上（版本衝突本身另有測試）。
/// 查無此列時回有效的版本值：測試要走到「找不到」，不是被請求驗證擋下。
/// </summary>
internal static class AdminVersions
{
    private const long Missing = 1;

    public static async Task<long> FeatureVersionAsync(this AppFactory factory, string id)
        => await ReadAsync(factory, db => db.Features.Where(x => x.Id == id).Select(x => (long?)x.RowVersion).SingleOrDefaultAsync()) ?? Missing;

    public static async Task<long> NavigationVersionAsync(this AppFactory factory, string id)
        => await ReadAsync(factory, db => db.NavigationItems.Where(x => x.Id == id).Select(x => (long?)x.RowVersion).SingleOrDefaultAsync()) ?? Missing;

    public static async Task<long> UnitVersionAsync(this AppFactory factory, Guid id)
        => await ReadAsync(factory, db => db.Units.Where(x => x.Id == id).Select(x => (long?)x.RowVersion).SingleOrDefaultAsync()) ?? Missing;

    public static async Task<int> ProfileVersionAsync(this AppFactory factory, Guid id)
        => await ReadAsync(factory, db => db.Users.Where(x => x.Id == id).Select(x => (int?)x.ProfileVersion).SingleOrDefaultAsync()) ?? (int)Missing;

    /// <summary>角色與群組的修改是 upsert：還不存在時回 null，也就是新增。</summary>
    public static Task<long?> RoleVersionAsync(this AppFactory factory, string id)
        => ReadAsync(factory, db => db.Roles.Where(x => x.Id == id).Select(x => (long?)x.RowVersion).SingleOrDefaultAsync());

    public static Task<long?> GroupVersionAsync(this AppFactory factory, string id)
        => ReadAsync(factory, db => db.RoleGroups.Where(x => x.Id == id).Select(x => (long?)x.RowVersion).SingleOrDefaultAsync());

    private static async Task<T> ReadAsync<T>(AppFactory factory, Func<AppDbContext, Task<T>> read)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await read(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}
