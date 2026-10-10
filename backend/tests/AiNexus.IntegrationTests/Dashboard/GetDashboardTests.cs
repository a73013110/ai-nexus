using System.Globalization;
using System.Net.Http.Json;
using AiNexus.Features.Account;
using AiNexus.Features.Attachments;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Dashboard;
using AiNexus.Features.Inference;
using AiNexus.Features.Knowledge.Documents;
using AiNexus.Features.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.Dashboard;

public sealed class GetDashboardTests
{
    [Fact]
    public async Task SavedOriginalsAreSeparateFromDocumentRecordsAndRespectOwnerScope()
    {
        await using var factory = new NexusFactory(administrators: ["alice"]);
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

    [Fact]
    public async Task TokenReportsIncludeLegacyAndBackgroundUsagePerModelAndRespectTimezoneAndOwner()
    {
        await using var factory = new NexusFactory(); using var owner = await factory.SignedInAsync(); using var other = await factory.SignedInAsync("bob");
        var id = (await owner.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id; var otherId = (await other.GetFromJsonAsync<MeDto>("/api/v1/me"))!.Id;
        var at = DateTimeOffset.Parse("2026-09-30T18:00:00Z", CultureInfo.InvariantCulture);
        using (var scope = factory.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
            db.AddRange(new ModelInvocation { OwnerId = id, Kind = "repository-review", ModelId = "test-model", Status = "completed", InputTokens = 120, OutputTokens = 30, CreatedAt = at }, new ModelInvocation { OwnerId = id, Kind = "ocr", ModelId = "another", Status = "failed", CreatedAt = at }, new ModelInvocation { OwnerId = otherId, ModelId = "test-model", InputTokens = 999, OutputTokens = 999, CreatedAt = at }); await db.SaveChangesAsync(); }
        var dashboard = (await owner.GetFromJsonAsync<DashboardDto>("/api/v1/dashboard?scope=personal&from=2026-09-30T00:00:00Z&until=2026-10-02T00:00:00Z&offset=480"))!;
        Assert.NotNull(dashboard.Tokens); Assert.Equal(150, dashboard.Tokens!.Daily.Sum(x => x.InputTokens + x.OutputTokens)); Assert.All(dashboard.Tokens.Daily, x => Assert.Equal("2026-10-01", x.Date));
        Assert.Equal(2, dashboard.Tokens.Daily.Sum(x => x.Requests)); Assert.Equal(1, dashboard.Tokens.Daily.Sum(x => x.RequestsWithUsage));
        Assert.Equal("測試模型", dashboard.Tokens.Daily.Single(x => x.ModelId == "test-model").ModelDisplayName);
        Assert.Equal("已停用的模型", dashboard.Tokens.Daily.Single(x => x.ModelId == "another").ModelDisplayName);
    }
}
