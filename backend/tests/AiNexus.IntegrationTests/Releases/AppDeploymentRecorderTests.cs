using System.Net.Http.Json;
using AiNexus.Data.App.Releases;
using AiNexus.Features.Admin.Releases.Versions;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.ReleaseFixtures;

namespace AiNexus.IntegrationTests.Releases;

public sealed class AppDeploymentRecorderTests
{
    [Fact]
    public async Task EachPackagedVersionIsRecordedOnceInTheDeploymentHistory()
    {
        await using var factory = new AppFactory(administrators: ["alice"]);
        using var alice = await factory.SignedInAsync();
        await DeployDatabaseAsync(factory, "2026.11.3");

        var first = Recorder(factory, Packaged("2026.11.4"));
        Assert.NotNull(await first.RecordAsync(CancellationToken.None));
        // 重新啟動（IIS 回收）同一版不再記錄。
        await Recorder(factory, Packaged("2026.11.4")).RecordAsync(CancellationToken.None);
        await Recorder(factory, Packaged("2026.11.5")).RecordAsync(CancellationToken.None);

        var history = (await alice.GetFromJsonAsync<DeploymentDto[]>("/api/v1/admin/deployments", TestContext.Current.CancellationToken))!;
        Assert.Equal(
            [(DeploymentComponents.App, "2026.11.5"), (DeploymentComponents.App, "2026.11.4"), (DeploymentComponents.Database, "2026.11.3")],
            history.Select(x => (x.Component, x.Version)));
        Assert.Equal((Revision, Environment.MachineName), (history[0].Revision, history[0].Host));
    }

    private static AppDeploymentRecorder Recorder(AppFactory factory, Features.Admin.Releases.AppRelease release)
        => ActivatorUtilities.CreateInstance<AppDeploymentRecorder>(factory.Services, release);
}
