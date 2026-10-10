using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Account;
using AiNexus.Features.Knowledge.Documents;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.ProjectApi;

namespace AiNexus.IntegrationTests.Projects;

public sealed class ListProjectFilesTests
{
    [Fact]
    public async Task ProjectFilesMatchEachDocumentDetailForOwnerViewerAndEditorInFixedQueries()
    {
        await using var factory = new NexusFactory(services: RequestQueries.Register);
        using var alice = await factory.SignedInAsync();
        using var bob = await factory.SignedInAsync("bob");
        var bobId = (await bob.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var project = await CreateProject(alice, "參考檔案");
        var id = project.Resource.Id; var path = $"/api/v1/projects/{id}/files";
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync(path)).StatusCode);
        await AddProjectFile(alice, id, "first.txt");
        var queries = factory.Services.GetRequiredService<RequestQueries>();
        queries.Clear(); (await alice.GetAsync(path)).EnsureSuccessStatusCode(); var single = queries.Count(path);
        await AddProjectFile(alice, id, "second.txt"); await AddProjectFile(alice, id, "third.txt");
        queries.Clear(); (await alice.GetAsync(path)).EnsureSuccessStatusCode();
        Assert.Equal(single, queries.Count(path));

        await AssertMatchesDetail(alice, path, canEdit: true);
        await Share(alice, $"/api/v1/projects/{id}/access", (bobId, "viewer"));
        await AssertMatchesDetail(bob, path, canEdit: false);
        await Share(alice, $"/api/v1/projects/{id}/access", (bobId, "editor"));
        await AssertMatchesDetail(bob, path, canEdit: true);

        static async Task AssertMatchesDetail(HttpClient client, string path, bool canEdit)
        {
            var list = (await client.GetFromJsonAsync<DocumentDto[]>(path))!;
            Assert.Equal(3, list.Length);
            foreach (var item in list)
            {
                Assert.Equal(canEdit, item.CanEdit);
                Assert.Equal(await client.GetFromJsonAsync<DocumentDto>($"/api/v1/documents/{item.Id}"), item);
            }
        }
    }
}
