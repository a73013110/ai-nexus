using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Attachments;
using AiNexus.Features.Jobs;
using AiNexus.Features.Knowledge.Documents;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Writer;
using static AiNexus.IntegrationTests.Support.AttachmentApi;
using static AiNexus.IntegrationTests.Support.BackgroundJobs;
using static AiNexus.IntegrationTests.Support.ChatApi;
using static AiNexus.IntegrationTests.Support.KnowledgeApi;

namespace AiNexus.IntegrationTests.Knowledge;

public sealed class DocumentIndexingTests
{
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAIAAACQkWg2AAAAIUlEQVR4nGP8z0AaYCJRPcOoBmIAE1GqkMCoBmIAyaEEAEAuAR9UPEsJAAAAAElFTkSuQmCC");

    [Fact]
    public async Task FailedIndexResumesPersistedPagesAndExpiredLeaseCannotDoubleProcess()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync(); var collection = await CreateCollection(client);
        var document = await AddDocument(client, collection.Resource.Id, new string('文', 1300));
        factory.Embeddings.Fail = true; await Process(factory);
        document = (await client.GetFromJsonAsync<DocumentDto>($"/api/v1/documents/{document.Id}"))!;
        var failed = (await client.GetFromJsonAsync<JobDto>($"/api/v1/jobs/{document.JobId}"))!;
        Assert.Equal("failed", failed.Status); Assert.Equal("fixture_embedding_failed", failed.ErrorCode);
        Assert.Single((await client.GetFromJsonAsync<DocumentPageDto[]>($"/api/v1/documents/{document.Id}/pages"))!);
        factory.Embeddings.Fail = false;
        (await client.PostAsync($"/api/v1/jobs/{document.JobId}/retry", null)).EnsureSuccessStatusCode();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            var job = await db.Set<BackgroundJob>().SingleAsync(x => x.Id == document.JobId); job.Status = "running"; job.LeaseToken = Guid.NewGuid(); job.LeaseUntil = DateTimeOffset.UtcNow.AddMinutes(-1); await db.SaveChangesAsync();
        }
        using var first = ActivatorUtilities.CreateInstance<BackgroundJobWorker>(factory.Services);
        using var second = ActivatorUtilities.CreateInstance<BackgroundJobWorker>(factory.Services);
        await Task.WhenAll(first.ProcessNextAsync(CancellationToken.None), second.ProcessNextAsync(CancellationToken.None));
        var done = (await client.GetFromJsonAsync<JobDto>($"/api/v1/jobs/{document.JobId}"))!;
        Assert.Equal("completed", done.Status); Assert.Equal(2, done.Attempt);
        Assert.Equal(2, (await client.GetFromJsonAsync<DocumentDto>($"/api/v1/documents/{document.Id}"))!.ChunkCount);
        Assert.Equal(2, factory.Embeddings.Calls); // one failed batch and one resumed batch
    }

    [Fact]
    public async Task ScannedPdfUsesVisionOcrBeforeItCanEnterChatContext()
    {
        await using var factory = new NexusFactory(inference: x => x.Models[0].SupportsImages = true);
        using var client = await factory.SignedInAsync();
        var builder = new PdfDocumentBuilder(); builder.AddPage(300, 400).AddPng(Png, new PdfRectangle(0, 0, 300, 400));
        var file = await Upload(client, "scan.pdf", builder.Build()); Assert.Equal("ocr-required", file.AnalysisMode);
        var conversation = await CreateConversation(client);
        Assert.Equal(HttpStatusCode.Conflict, (await PostRun(client, new(conversation.Id, "test-model", "Read", null, null, AttachmentIds: [file.Id]))).StatusCode);
        var response = await client.PostAsync($"/api/v1/attachments/{file.Id}/document", null); response.EnsureSuccessStatusCode(); var document = (await response.Content.ReadFromJsonAsync<DocumentDto>())!;
        await Process(factory);
        Assert.Equal("ready", (await client.GetFromJsonAsync<DocumentDto>($"/api/v1/documents/{document.Id}"))!.Status);
        var page = Assert.Single((await client.GetFromJsonAsync<DocumentPageDto[]>($"/api/v1/documents/{document.Id}/pages"))!); Assert.Equal("ocr", page.Extraction); Assert.True(page.NeedsReview);
        Assert.Equal(["system", "user"], factory.Provider.LastMessages.Select(x => x.Role));
        Assert.Single(factory.Provider.LastMessages.Single(x => x.Role == "user").Images!);
        Assert.Equal("extracted-text", (await client.GetFromJsonAsync<AttachmentDto>($"/api/v1/attachments/{file.Id}"))!.AnalysisMode);
    }
}
