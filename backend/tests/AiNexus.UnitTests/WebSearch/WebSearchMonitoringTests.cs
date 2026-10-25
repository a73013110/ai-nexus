using AiNexus.Features.Platform.WebSearch;
using AiNexus.Platform.Monitoring;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.UnitTests.WebSearch;

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
            ["WebSearch:Enabled"] = enabled.ToString(), ["WebSearch:Provider"] = provider, ["WebSearch:ApiKey"] = apiKey,
        });
        WebSearchModule.AddServices(builder);
        await using var services = builder.Services.BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();
        var source = scope.ServiceProvider.GetServices<MonitoringSourceRegistration>()
            .Select(x => (IMonitoringSource)scope.ServiceProvider.GetRequiredService(x.Type)).Single(x => x.Id == "web-search");

        var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(expected, reading.Status);
        Assert.Equal(setup, Assert.Single(reading.Facts, x => x.Id == "setup").Text);
    }
}
