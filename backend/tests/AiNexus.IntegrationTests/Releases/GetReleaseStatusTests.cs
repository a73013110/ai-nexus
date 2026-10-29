using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Admin.Releases.Versions;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.ProblemAssertions;
using static AiNexus.IntegrationTests.Support.ReleaseFixtures;

namespace AiNexus.IntegrationTests.Releases;

public sealed class GetReleaseStatusTests
{
    private const string Status = "/api/v1/admin/release-status";

    [Fact]
    public async Task TheDatabaseIsIncompatibleUntilItReachesTheVersionTheAppNeeds()
    {
        await using var factory = new AppFactory(administrators: ["alice"], services: services => services.AddSingleton(Packaged("2026.11.6", "2026.11.4")));
        using var alice = await factory.SignedInAsync();

        // 從未執行過 update.sql 的庫沒有版本紀錄，程式又有要求：不能當成相容。
        var status = (await alice.GetFromJsonAsync<ReleaseStatusDto>(Status, TestContext.Current.CancellationToken))!;
        Assert.Equal((null, "incompatible", false), (status.DatabaseVersion, status.Compatibility, status.HasSnapshot));

        await DeployDatabaseAsync(factory, "2026.11.3");
        status = (await alice.GetFromJsonAsync<ReleaseStatusDto>(Status, TestContext.Current.CancellationToken))!;
        Assert.Equal(("2026.11.3", "2026.11.4", "incompatible"), (status.DatabaseVersion, status.MinimumDatabaseVersion, status.Compatibility));

        // 資料庫可以比程式新：資料庫先發，程式還在跑內規流程。
        await DeployDatabaseAsync(factory, "2026.11.5");
        status = (await alice.GetFromJsonAsync<ReleaseStatusDto>(Status, TestContext.Current.CancellationToken))!;
        Assert.Equal(("2026.11.6", "2026.11.5", "compatible"), (status.AppVersion, status.DatabaseVersion, status.Compatibility));
    }

    [Fact]
    public async Task UnpackagedBuildsAreNotCheckedAndMembersCannotReadTheVersionPage()
    {
        await using var factory = new AppFactory(administrators: ["alice"]);
        using var alice = await factory.SignedInAsync();
        var status = (await alice.GetFromJsonAsync<ReleaseStatusDto>(Status, TestContext.Current.CancellationToken))!;
        Assert.Equal(("0.0.0-dev", "unchecked"), (status.AppVersion, status.Compatibility));

        using var member = await factory.SignedInAsync("bob");
        await AssertCodeAsync(await member.GetAsync(Status, TestContext.Current.CancellationToken), HttpStatusCode.Forbidden, "feature_forbidden");
        await AssertCodeAsync(await member.GetAsync("/api/v1/admin/deployments", TestContext.Current.CancellationToken), HttpStatusCode.Forbidden, "feature_forbidden");
        await AssertCodeAsync(await member.GetAsync("/api/v1/admin/schema-changes", TestContext.Current.CancellationToken), HttpStatusCode.Forbidden, "feature_forbidden");
    }
}
