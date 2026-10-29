using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Admin.Releases;
using AiNexus.Features.Admin.Releases.Versions;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.ReleaseFixtures;

namespace AiNexus.IntegrationTests.Releases;

public sealed class GetCurrentReleaseTests
{
    [Fact]
    public async Task SignedInUsersSeeTheRunningVersionAndEveryApiResponseCarriesIt()
    {
        var release = Packaged("2026.11.4", "2026.11.3", new ReleaseNoteDto("feat", "chat", "對話可以釘選"), new ReleaseNoteDto("fix", "", "登入逾時後保留草稿"));
        await using var factory = new AppFactory(services: services => services.AddSingleton(release));
        using var member = await factory.SignedInAsync("bob");

        var response = await member.GetAsync("/api/v1/releases/current", TestContext.Current.CancellationToken);
        var current = (await response.Content.ReadFromJsonAsync<CurrentReleaseDto>(TestContext.Current.CancellationToken))!;
        Assert.Equal(("2026.11.4", Revision), (current.Version, current.Revision));
        Assert.Equal(["對話可以釘選", "登入逾時後保留草稿"], current.Changes.Select(x => x.Subject));
        Assert.Equal("2026.11.4", Assert.Single(response.Headers.GetValues(AppVersionHeader.Name)));
        // 版本不是秘密，但沒必要告訴未登入的人。
        using var anonymous = factory.CreateClient();
        var rejected = await anonymous.GetAsync("/api/v1/releases/current", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        Assert.False(rejected.Headers.Contains(AppVersionHeader.Name));
    }

    [Fact]
    public async Task UnpackagedBuildsHaveNoReleaseNotes()
    {
        await using var factory = new AppFactory();
        using var member = await factory.SignedInAsync("bob");
        var current = (await member.GetFromJsonAsync<CurrentReleaseDto>("/api/v1/releases/current", TestContext.Current.CancellationToken))!;
        Assert.Equal("0.0.0-dev", current.Version);
        Assert.Null(current.BuiltAt);
        Assert.Empty(current.Changes);
    }
}
