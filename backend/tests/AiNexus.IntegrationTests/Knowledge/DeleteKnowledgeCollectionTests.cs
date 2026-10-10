using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Attachments;
using AiNexus.Features.Knowledge.Collections;
using AiNexus.Features.Knowledge.Documents;
using AiNexus.Features.Knowledge.Indexing;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AiNexus.IntegrationTests.Support.BackgroundJobs;
using static AiNexus.IntegrationTests.Support.ChatApi;
using static AiNexus.IntegrationTests.Support.KnowledgeApi;

namespace AiNexus.IntegrationTests.Knowledge;

public sealed class DeleteKnowledgeCollectionTests
{
    [Fact]
    public async Task DeletingCollectionPurgesIndexAndUnlinksSourcesButRetainsLibraryOriginal()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync(); var collection = await CreateCollection(client);
        var doc = await AddDocument(client, collection.Resource.Id, "Content removed with the collection."); await Process(factory);
        var conversation = await CreateConversation(client);
        (await client.PutAsJsonAsync($"/api/v1/conversations/{conversation.Id}/knowledge", new KnowledgeSelectionDto([collection.Resource.Id]))).EnsureSuccessStatusCode();
        (await client.DeleteAsync($"/api/v1/knowledge/collections/{collection.Resource.Id}")).EnsureSuccessStatusCode();
        Assert.Empty((await client.GetFromJsonAsync<KnowledgeSelectionDto>($"/api/v1/conversations/{conversation.Id}/knowledge"))!.CollectionIds);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/documents/{doc.Id}/content")).StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        Assert.False(await db.Set<DocumentPage>().AnyAsync()); Assert.False(await db.Set<KnowledgeChunk>().AnyAsync());
        var file = Assert.Single(await db.Set<Attachment>().ToListAsync()); Assert.True(file.InLibrary);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/attachments/{file.Id}/content")).StatusCode);
    }
}
