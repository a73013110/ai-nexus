using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Account;
using AiNexus.Features.Chat;
using AiNexus.Features.Persistence;
using AiNexus.Features.Artifacts;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Projects;
using AiNexus.Features.Attachments;
using AiNexus.Features.Quality;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using AiNexus.Features.Knowledge.Documents;

namespace AiNexus.Tests;

public sealed class ResourceLifecycleTests
{
    [Fact]
    public async Task DeletingProjectIsOwnerOnlyAndPreservesPrivateWorkWithoutInheritedGrants()
    {
        await using var f = new NexusFactory(); using var owner = await f.SignedInAsync(); using var editor = await f.SignedInAsync("bob");
        var project = (await (await owner.PostAsJsonAsync("/api/v1/projects", new ProjectRequest("container"))).Content.ReadFromJsonAsync<ProjectDto>())!;
        var id = project.Resource.Id;
        var bob = (await editor.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        (await owner.PutAsJsonAsync($"/api/v1/projects/{id}/access", new ResourceAclRequest([new(bob.Id, "editor")], []))).EnsureSuccessStatusCode();
        var conversation = (await (await editor.PostAsJsonAsync($"/api/v1/projects/{id}/conversations", new ProjectConversationRequest())).Content.ReadFromJsonAsync<ProjectConversationDto>())!;
        var artifact = (await (await owner.PostAsJsonAsync($"/api/v1/projects/{id}/artifacts", new CreateArtifactRequest("kept", "body"))).Content.ReadFromJsonAsync<ArtifactDto>())!;
        using var upload = new MultipartFormDataContent(); upload.Add(new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("retained text")), "file", "retained.txt");
        var attachment = (await (await owner.PostAsync("/api/v1/attachments", upload)).Content.ReadFromJsonAsync<AttachmentDto>())!;
        var document = (await (await owner.PostAsJsonAsync($"/api/v1/projects/{id}/files", new AddDocumentRequest(attachment.Id))).Content.ReadFromJsonAsync<DocumentDto>())!;
        Assert.Equal(HttpStatusCode.NotFound, (await editor.DeleteAsync($"/api/v1/projects/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.DeleteAsync($"/api/v1/projects/{id}")).StatusCode);
        await ActivatorUtilities.CreateInstance<AiNexus.Features.Jobs.BackgroundJobWorker>(f.Services).ProcessNextAsync(default);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/v1/projects/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/v1/projects/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await editor.GetAsync($"/api/v1/artifacts/{artifact.Resource.Id}")).StatusCode);
        Assert.Equal("body", (await owner.GetFromJsonAsync<ArtifactDto>($"/api/v1/artifacts/{artifact.Resource.Id}"))!.Content);
        var detail = (await editor.GetFromJsonAsync<ConversationDetailDto>($"/api/v1/conversations/{conversation.Conversation.Id}"))!;
        Assert.Null(detail.Conversation.ProjectId);
        Assert.Equal(attachment.Id, Assert.Single((await owner.GetFromJsonAsync<FileLibraryPageDto>("/api/v1/files"))!.Items).File.Id);
        Assert.Empty((await editor.GetFromJsonAsync<FileLibraryPageDto>("/api/v1/files"))!.Items);
        Assert.Equal(HttpStatusCode.NotFound, (await editor.GetAsync($"/api/v1/documents/{document.Id}")).StatusCode);
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        Assert.Single(await db.AuditEvents.Where(x => x.ResourceId == id && x.Action == "project.deleted").ToArrayAsync());
    }
    [Fact]
    public async Task EvaluationDeletionRejectsActiveJobsAndRetainsItsFrozenResults()
    {
        await using var f = new NexusFactory(); using var owner = await f.SignedInAsync();
        var set = (await (await owner.PostAsJsonAsync("/api/v1/quality/sets", new EvaluationSetRequest("fixture", "", [new("question")]))).Content.ReadFromJsonAsync<EvaluationSetDto>())!;
        var run = (await (await owner.PostAsJsonAsync($"/api/v1/quality/sets/{set.Resource.Id}/runs", new EvaluationRunRequest([new("default", "test-model", "")]))).Content.ReadFromJsonAsync<EvaluationRunDto>())!;
        Assert.Equal(HttpStatusCode.Conflict, (await owner.DeleteAsync($"/api/v1/quality/sets/{set.Resource.Id}")).StatusCode);
        await ActivatorUtilities.CreateInstance<AiNexus.Features.Jobs.BackgroundJobWorker>(f.Services).ProcessNextAsync(default);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/v1/quality/sets/{set.Resource.Id}")).StatusCode);
        Assert.Empty((await owner.GetFromJsonAsync<EvaluationSetDto[]>("/api/v1/quality/sets"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/v1/quality/runs/{run.Id}")).StatusCode);
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        Assert.True(await db.Set<EvaluationResult>().AnyAsync(x => x.RunId == run.Id));
    }
}
