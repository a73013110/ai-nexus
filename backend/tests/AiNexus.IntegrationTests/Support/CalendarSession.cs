using AiNexus.Data.App;
using AiNexus.Features.Platform.Gd;
using AiNexus.Platform.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.Support;

/// <summary>
/// 假日設定的測試環境：一個假 GD、一個 host，以及一位有假日設定全部功能的 HR。HR 的 AD 帳號對到假 GD 的公文帳號 <see cref="HrGdId"/>。
/// 參數模擬 GD 的部署狀態：未設定、連不上，或設為 <c>ReadOnly=true</c>（只能查詢、不能回寫）。
/// </summary>
internal sealed class CalendarSession : IAsyncDisposable
{
    internal const string HrAdAccount = "hr.chen";
    internal const string HrGdId = "G-HR";

    public required FakeGd Gd { get; init; }
    public required AppFactory Factory { get; init; }
    public required HttpClient Hr { get; init; }
    public required Guid HrId { get; init; }

    /// <param name="configured">false 時沒有 <c>Databases:Gd</c>。</param>
    /// <param name="reachable">false 時指向不存在的資料庫，連線一定失敗。</param>
    /// <param name="readOnly">null 沿用 <c>appsettings.json</c> 的預設，模擬部署沒有另外設定 <c>ReadOnly</c>。</param>
    public static async Task<CalendarSession> CreateAsync(bool configured = true, bool reachable = true, bool? readOnly = null)
    {
        var gd = FakeGd.Create();
        var factory = new AppFactory(services: services => services.PostConfigure<DatabasesOptions>(options =>
        {
            var connection = reachable ? gd.Databases[GdDatabase.Name] : FakeGd.Missing();
            connection.ReadOnly = readOnly ?? options.For(GdDatabase.Name).ReadOnly;
            if (configured) options[GdDatabase.Name] = connection;
            else options.Remove(GdDatabase.Name);
        }));
        var hrId = await OrganizationApi.UserIdAsync(factory, "hr");
        await SetAdAccountAsync(factory, hrId, HrAdAccount);
        gd.AddUser(HrGdId, HrAdAccount);
        await AttendanceApi.GrantCalendarAsync(factory, hrId);
        return new() { Gd = gd, Factory = factory, Hr = await factory.SignedInAsync("hr"), HrId = hrId };
    }

    /// <summary>
    /// 直接改寫 AD 帳號（與測試登入名不同）；登入的快速路徑不比對改名，5 分鐘內的登入不會改回。
    /// 清空時補上本地帳號：資料表要求 AD 帳號與本地帳號至少一個。
    /// </summary>
    public static async Task SetAdAccountAsync(AppFactory factory, Guid userId, string? adAccount)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.Where(x => x.Id == userId).ExecuteUpdateAsync(x => x
            .SetProperty(u => u.LocalAccount, u => adAccount == null ? u.LocalAccount ?? "no-ad-account" : u.LocalAccount)
            .SetProperty(u => u.AdAccount, adAccount));
    }

    public async ValueTask DisposeAsync()
    {
        Hr.Dispose();
        await Factory.DisposeAsync();
        Gd.Dispose();
    }
}
