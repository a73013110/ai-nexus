using System.Net.Http.Json;
using AiNexus.Features.Account;
using AiNexus.Features.Projects;
using AiNexus.Features.Quality.Evaluations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.ProjectApi;

namespace AiNexus.IntegrationTests.Collaboration;

public sealed class ResourceAccessTests
{
    [Fact]
    public async Task ProjectAndEvaluationSetListsMatchEachItemAndMarkOnlyEditableItems()
    {
        await using var factory = new NexusFactory(services: RequestQueries.Register);
        using var alice = await factory.SignedInAsync();
        using var bob = await factory.SignedInAsync("bob");
        using var carol = await factory.SignedInAsync("carol");
        var aliceId = (await alice.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var own = await CreateProject(alice, "自己的");
        var viewed = await CreateProject(bob, "只能看");
        var edited = await CreateProject(carol, "可編輯");
        await CreateProject(carol, "看不到");
        var queries = factory.Services.GetRequiredService<RequestQueries>();
        await Share(bob, $"/api/v1/projects/{viewed.Resource.Id}/access", (aliceId, "viewer"));
        queries.Clear(); (await alice.GetAsync("/api/v1/projects")).EnsureSuccessStatusCode(); var fewer = queries.Count("/api/v1/projects");
        await Share(carol, $"/api/v1/projects/{edited.Resource.Id}/access", (aliceId, "editor"));
        // The list costs the same however many shared projects it describes.
        queries.Clear(); (await alice.GetAsync("/api/v1/projects")).EnsureSuccessStatusCode();
        Assert.Equal(fewer, queries.Count("/api/v1/projects"));
        var projects = (await alice.GetFromJsonAsync<ProjectDto[]>("/api/v1/projects"))!;
        Assert.Equal(new[] { edited.Resource.Id, viewed.Resource.Id, own.Resource.Id }.Order(), projects.Select(x => x.Resource.Id).Order());
        Assert.Equal([true, false, true], new[] { own, viewed, edited }.Select(p => projects.Single(x => x.Resource.Id == p.Resource.Id).Resource.CanEdit));
        foreach (var item in projects) Assert.Equal(await alice.GetFromJsonAsync<ProjectDto>($"/api/v1/projects/{item.Resource.Id}"), item);

        async Task<EvaluationSetDto> CreateSet(HttpClient client, string name)
        {
            using var response = await client.PostAsJsonAsync("/api/v1/quality/sets", new EvaluationSetRequest(name, "說明", [new EvaluationCase("採購需要誰核准？")]));
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<EvaluationSetDto>())!;
        }
        var mine = await CreateSet(alice, "我的題庫");
        var readOnly = await CreateSet(bob, "唯讀題庫");
        var editable = await CreateSet(carol, "共編題庫");
        await CreateSet(carol, "私人題庫");
        await Share(bob, $"/api/v1/quality/sets/{readOnly.Resource.Id}/access", (aliceId, "viewer"));
        await Share(carol, $"/api/v1/quality/sets/{editable.Resource.Id}/access", (aliceId, "editor"));
        var sets = (await alice.GetFromJsonAsync<EvaluationSetDto[]>("/api/v1/quality/sets"))!;
        Assert.Equal(3, sets.Length);
        Assert.Equal([true, false, true], new[] { mine, readOnly, editable }.Select(s => sets.Single(x => x.Resource.Id == s.Resource.Id).Resource.CanEdit));
        foreach (var item in sets)
        {
            var detail = (await alice.GetFromJsonAsync<EvaluationSetDto>($"/api/v1/quality/sets/{item.Resource.Id}"))!;
            Assert.Equal(detail.Resource, item.Resource); Assert.Equal(detail.Version, item.Version); Assert.Equal(detail.Cases.Single(), item.Cases.Single());
        }
    }
}
