using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Jobs;
using static AiNexus.IntegrationTests.Support.BackgroundJobs;
using static AiNexus.IntegrationTests.Support.KnowledgeApi;

namespace AiNexus.IntegrationTests.Jobs;

public sealed class JobLifecycleTests
{
    [Fact]
    public async Task JobsAreOwnerScopedCancellationIsDurableAndRetryRequiresCsrf()
    {
        await using var factory = new NexusFactory();
        using var alice = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        var collection = await CreateCollection(alice); var document = await AddDocument(alice, collection.Resource.Id, "Cancellation sample document.");
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PostAsync($"/api/v1/jobs/{document.JobId}/cancel", null)).StatusCode);
        (await alice.PostAsync($"/api/v1/jobs/{document.JobId}/cancel", null)).EnsureSuccessStatusCode(); await Process(factory);
        Assert.Equal("cancelled", (await alice.GetFromJsonAsync<JobDto>($"/api/v1/jobs/{document.JobId}"))!.Status);
        Assert.Equal(0, factory.Embeddings.Calls);
        alice.DefaultRequestHeaders.Remove("X-Nexus-CSRF");
        Assert.Equal(HttpStatusCode.Forbidden, (await alice.PostAsync($"/api/v1/jobs/{document.JobId}/retry", null)).StatusCode);
    }
}
