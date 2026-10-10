using System.Net.Http.Json;
using System.Text;
using AiNexus.Features.Account;
using AiNexus.Features.Jobs;
using AiNexus.Features.Knowledge.Collections;
using AiNexus.Features.Knowledge.Documents;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.AttachmentApi;

namespace AiNexus.IntegrationTests.Support;

internal static class KnowledgeApi
{
    internal static async Task<CollectionDto> CreateCollection(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/knowledge/collections", new CollectionRequest("公司作業規範", "受控測試來源")); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CollectionDto>())!;
    }

    internal static async Task<DocumentDto> AddDocument(HttpClient client, Guid collection, string text)
    {
        var file = await Upload(client, "policy.txt", Encoding.UTF8.GetBytes(text));
        using var response = await client.PostAsJsonAsync($"/api/v1/knowledge/collections/{collection}/documents", new AddDocumentRequest(file.Id)); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DocumentDto>())!;
    }

    internal static async Task<(Guid Collection, Guid Actor, DocumentDto Document)> SeedAsync(NexusFactory factory, HttpClient client)
    {
        using var created = await client.PostAsJsonAsync("/api/v1/knowledge/collections", new CollectionRequest("採購規範", "測試")); created.EnsureSuccessStatusCode();
        var collection = (await created.Content.ReadFromJsonAsync<CollectionDto>())!.Resource.Id;
        using var added = await client.PostAsJsonAsync($"/api/v1/knowledge/collections/{collection}/text", new TextDocumentRequest("採購規範", "採購應先核准。主管簽署後才可付款。")); added.EnsureSuccessStatusCode();
        var document = (await added.Content.ReadFromJsonAsync<DocumentDto>())!;
        using var worker = ActivatorUtilities.CreateInstance<BackgroundJobWorker>(factory.Services); Assert.True(await worker.ProcessNextAsync(CancellationToken.None));
        while (await worker.ProcessNextAsync(CancellationToken.None)) { }
        var actor = (await client.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        return (collection, actor, document);
    }
}
