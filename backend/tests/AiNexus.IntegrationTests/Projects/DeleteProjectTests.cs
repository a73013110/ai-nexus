using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Account;
using AiNexus.Features.Artifacts;
using AiNexus.Features.Attachments;
using AiNexus.Features.Chat;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Knowledge.Documents;
using AiNexus.Features.Persistence;
using AiNexus.Features.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiNexus.IntegrationTests.Projects;

public sealed class DeleteProjectTests
{
    [Fact]
    public async Task DeletingAProjectDetachesItsArtifacts()
    {
        await using var f = new NexusFactory(); using var owner = await f.SignedInAsync();
        var project = (await (await owner.PostAsJsonAsync("/api/v1/projects", new ProjectRequest("container"))).Content.ReadFromJsonAsync<ProjectDto>())!;
        var artifact = (await (await owner.PostAsJsonAsync($"/api/v1/projects/{project.Resource.Id}/artifacts", new CreateArtifactRequest("kept", "body"))).Content.ReadFromJsonAsync<ArtifactDto>())!;
        using (var scope = f.Services.CreateScope())
            Assert.Equal(project.Resource.Id, (await scope.ServiceProvider.GetRequiredService<NexusDbContext>().Set<Artifact>().SingleAsync(x => x.Id == artifact.Resource.Id)).ProjectId);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/v1/projects/{project.Resource.Id}")).StatusCode);
        using (var scope = f.Services.CreateScope())
            Assert.Null((await scope.ServiceProvider.GetRequiredService<NexusDbContext>().Set<Artifact>().SingleAsync(x => x.Id == artifact.Resource.Id)).ProjectId);
        Assert.Equal("body", (await owner.GetFromJsonAsync<ArtifactDto>($"/api/v1/artifacts/{artifact.Resource.Id}"))!.Content);
    }

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
}
