using System.Net.Http.Json;
using AiNexus.BuildingBlocks;
using AiNexus.Modules.Attachments;
using AiNexus.Modules.Collaboration;
using AiNexus.Modules.Dashboard;
using AiNexus.Modules.Inference;
using AiNexus.Modules.Knowledge;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AiNexus.Tests;

public sealed class DashboardTests
{
    [Fact]
    public async Task SavedOriginalsAreSeparateFromDocumentRecordsAndRespectOwnerScope()
    {
        await using var factory = new NexusFactory(administrators: ["alice"], backgroundJobs: false);
        using var alice = await factory.SignedInAsync();
        using var bob = await factory.SignedInAsync("bob");
        var a = (await alice.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var b = (await bob.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            var original = new Attachment { Size = 1, OwnerId = a, FileName = "shared-original.txt", InLibrary = true };
            db.AddRange(original, new Attachment { Size = 1, OwnerId = a, InLibrary = true },
                new Attachment { Size = 1, OwnerId = a, InLibrary = false },
                new Attachment { Size = 1, OwnerId = a, InLibrary = true, StorageState = AttachmentStates.Pending },
                new Attachment { Size = 1, OwnerId = a, InLibrary = true, StorageState = AttachmentStates.Deleting },
                new Attachment { Size = 1, OwnerId = b, InLibrary = true });
            // Two independently processed sources reuse the same immutable original.
            foreach (var status in new[] { "ready", "failed" })
            {
                var resource = new WorkspaceResource { OwnerId = a, Kind = "document", Name = "processed-source" };
                db.Add(resource);
                db.Add(new KnowledgeDocument { Id = resource.Id, AttachmentId = original.Id, Status = status });
            }
            await db.SaveChangesAsync();
        }
        // Resource snapshots remain current even when the spend period is in the past.
        var period = "from=2020-01-01&until=2020-01-02";
        var personal = (await alice.GetFromJsonAsync<DashboardDto>("/api/v1/dashboard?" + period))!;
        Assert.Equal(2, personal.Counts.Files);
        Assert.Equal(2, personal.Counts.Documents);
        Assert.Equal(1, personal.Counts.ReadyDocuments);
        Assert.Equal(1, personal.Counts.FailedDocuments);
        Assert.Equal(0, personal.Counts.Collections);
        Assert.Equal(0, personal.Counts.Chunks);
        var other = (await bob.GetFromJsonAsync<DashboardDto>("/api/v1/dashboard?" + period))!;
        Assert.Equal(1, other.Counts.Files);
        Assert.Equal(0, other.Counts.Documents);
        var platform = (await alice.GetFromJsonAsync<DashboardDto>("/api/v1/dashboard?scope=platform&" + period))!;
        Assert.Equal(3, platform.Counts.Files);
        var selected = (await alice.GetFromJsonAsync<DashboardDto>($"/api/v1/dashboard?scope=platform&ownerId={b}&{period}"))!;
        Assert.Equal(1, selected.Counts.Files);
        Assert.Equal(0, selected.Counts.Documents);
    }
}
