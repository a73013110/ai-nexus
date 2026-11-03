using AiNexus.Features;
using AiNexus.Features.Platform.WebSearches;
using AiNexus.Platform;
using AiNexus.Platform.Monitoring;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.UnitTests.WebSearches;

/// <summary>監控卡片與聊天頁共用同一個可用判定：未啟用 idle、缺 Brave key degraded、其餘 healthy。</summary>
public sealed class WebSearchMonitoringTests
{
    [Theory]
    [InlineData(false, "searxng", "", SubsystemStates.Idle, "未啟用")]
    [InlineData(true, "brave", "", SubsystemStates.Degraded, "設定不完整")]
    [InlineData(true, "brave", "fixture", SubsystemStates.Healthy, "已設定")]
    [InlineData(true, "searxng", "", SubsystemStates.Healthy, "已設定")]
    public async Task StatusFollowsTheSameSetupCheckAsChat(bool enabled, string provider, string apiKey, string expected, string setup)
    {
        var builder = WebApplication.CreateEmptyBuilder(new());
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["WebSearches:Enabled"] = enabled.ToString(), ["WebSearches:Provider"] = provider, ["WebSearches:ApiKey"] = apiKey,
        });
        // 監控來源由組合產生器註冊（ADR 0033），要經過 AddFeatures 才會出現。
        // 只解析這個模組的來源：其他模組的來源需要各自的設定與資料庫。
        builder.AddPlatform();
        builder.AddFeatures();
        await using var services = builder.Services.BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();
        var registration = scope.ServiceProvider.GetServices<MonitoringSourceRegistration>().Single(x => x.Type.Namespace == typeof(WebSearchesModule).Namespace);
        var source = (IMonitoringSource)scope.ServiceProvider.GetRequiredService(registration.Type);

        var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(expected, reading.Status);
        Assert.Equal(setup, Assert.Single(reading.Facts, x => x.Id == "setup").Text);
    }
}
