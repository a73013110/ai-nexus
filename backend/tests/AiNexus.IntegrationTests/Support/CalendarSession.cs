using AiNexus.Data.App;
using AiNexus.Features.Admin.Integrations.Sources;
using AiNexus.Features.Staff.Attendance.Gd;
using AiNexus.Platform.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.Support;

/// <summary>
/// 假日設定的測試環境：一個假 GD、一個 host，以及一位有假日設定全部功能的 HR。HR 的 AD 帳號對到假 GD 的公文帳號 <see cref="HrGdId"/>。
/// 參數模擬 GD 的各種狀態：唯讀連線連不上、可寫入連線未設定或連不上。
/// </summary>
internal sealed class CalendarSession : IAsyncDisposable
{
    internal const string HrAdAccount = "hr.chen";
    internal const string HrGdId = "G-HR";

    public required FakeGd Gd { get; init; }
    public required AppFactory Factory { get; init; }
    public required HttpClient Hr { get; init; }
    public required Guid HrId { get; init; }

    public static async Task<CalendarSession> CreateAsync(bool readable = true, bool writableConfigured = true, bool writableReachable = true)
    {
        var gd = FakeGd.Create();
        var factory = new AppFactory(services: services => services.PostConfigure<DatabasesOptions>(options =>
        {
            options[GdDatabase.Name] = readable ? gd.Databases[GdDatabase.Name] : FakeGd.Missing(readOnly: true);
            if (writableConfigured) options[GdCalendarDatabase.Name] = writableReachable ? gd.Writable : FakeGd.Missing(readOnly: false);
        }));
        var hrId = await OrganizationApi.UserIdAsync(factory, "hr");
        await SetAdAccountAsync(factory, hrId, HrAdAccount);
        gd.AddUser(HrGdId, HrAdAccount);
        await AttendanceApi.GrantCalendarAsync(factory, hrId);
        return new() { Gd = gd, Factory = factory, Hr = await factory.SignedInAsync("hr"), HrId = hrId };
    }

    /// <summary>測試登入建立的使用者沒有 AD 帳號，直接寫入；之後的登入以 SID 對應，不會改掉它。</summary>
    public static async Task SetAdAccountAsync(AppFactory factory, Guid userId, string? adAccount)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.Where(x => x.Id == userId).ExecuteUpdateAsync(x => x.SetProperty(u => u.AdAccount, adAccount));
    }

    public async ValueTask DisposeAsync()
    {
        Hr.Dispose();
        await Factory.DisposeAsync();
        Gd.Dispose();
    }
}
