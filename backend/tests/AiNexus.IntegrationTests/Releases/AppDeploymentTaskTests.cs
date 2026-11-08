using System.Net.Http.Json;
using AiNexus.Data.App.Releases;
using AiNexus.Features.Admin.Releases.Versions;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.ReleaseFixtures;

namespace AiNexus.IntegrationTests.Releases;

public sealed class AppDeploymentTaskTests
{
    [Fact]
    public async Task EachPackagedVersionIsRecordedOnceInTheDeploymentHistory()
    {
        await using var factory = new AppFactory(administrators: ["alice"]);
        using var alice = await factory.SignedInAsync();
        await DeployDatabaseAsync(factory, "2026.11.3");

        Assert.True(await RecordAsync(factory, Packaged("2026.11.4")));
        // 重新啟動（IIS 回收）同一版不再記錄。
        Assert.False(await RecordAsync(factory, Packaged("2026.11.4")));
        Assert.True(await RecordAsync(factory, Packaged("2026.11.5")));

        var history = (await alice.GetFromJsonAsync<DeploymentDto[]>("/api/v1/admin/deployments", TestContext.Current.CancellationToken))!;
        Assert.Equal(
            [(DeploymentComponents.App, "2026.11.5"), (DeploymentComponents.App, "2026.11.4"), (DeploymentComponents.Database, "2026.11.3")],
            history.Select(x => (x.Component, x.Version)));
        Assert.Equal((Revision, Environment.MachineName), (history[0].Revision, history[0].Host));
    }

    private static async Task<bool> RecordAsync(AppFactory factory, Features.Admin.Releases.AppRelease release)
    {
        using var scope = factory.Services.CreateScope();
        return await ActivatorUtilities.CreateInstance<AppDeploymentTask>(scope.ServiceProvider, release).RecordAsync(CancellationToken.None);
    }
}
