using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Account;
using AiNexus.Features.Conversations;
using AiNexus.Features.Artifacts;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Projects;
using Xunit;
using static AiNexus.Tests.ChatApiTests;
namespace AiNexus.Tests;
public sealed class ProjectTests
{
    [Fact]
    public async Task ProjectMembershipScopesTemplatesFilesAndArtifactsWhileConversationsStayPrivate()
    {
        await using var f = new NexusFactory(); using var alice = await f.SignedInAsync(); using var bob = await f.SignedInAsync("bob");
        var project = (await (await alice.PostAsJsonAsync("/api/v1/projects", new ProjectRequest("專案一", "工作背景", "回答要核對來源"))).Content.ReadFromJsonAsync<ProjectDto>())!;
        var id = project.Resource.Id; var user = (await bob.GetFromJsonAsync<MeDto>("/api/v1/me"))!;
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/v1/projects/{id}")).StatusCode);
        var template = (await (await alice.PostAsJsonAsync($"/api/v1/projects/{id}/templates", new ProjectTemplateRequest("核對", "請核對數字"))).Content.ReadFromJsonAsync<ProjectTemplateDto>())!;
        var artifact = (await (await alice.PostAsJsonAsync($"/api/v1/projects/{id}/artifacts", new CreateArtifactRequest("專案成果", "原始內容"))).Content.ReadFromJsonAsync<ArtifactDto>())!;
        var conversation = (await (await alice.PostAsJsonAsync($"/api/v1/projects/{id}/conversations", new ProjectConversationRequest(TemplateId: template.Id))).Content.ReadFromJsonAsync<ProjectConversationDto>())!;
        (await alice.PutAsJsonAsync($"/api/v1/projects/{id}/access", new ResourceAclRequest([new(user.Id, "viewer")], []))).EnsureSuccessStatusCode();
        Assert.Single((await bob.GetFromJsonAsync<ProjectTemplateDto[]>($"/api/v1/projects/{id}/templates"))!);
        Assert.Empty((await bob.GetFromJsonAsync<ConversationDto[]>($"/api/v1/projects/{id}/conversations"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/v1/conversations/{conversation.Conversation.Id}")).StatusCode);
        Assert.Equal("原始內容", (await bob.GetFromJsonAsync<ArtifactDto>($"/api/v1/artifacts/{artifact.Resource.Id}"))!.Content);
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.PutAsJsonAsync($"/api/v1/artifacts/{artifact.Resource.Id}", new SaveArtifactRequest("成果", "應拒絕", 1))).StatusCode);
        (await alice.PutAsJsonAsync($"/api/v1/projects/{id}/access", new ResourceAclRequest([new(user.Id, "editor")], []))).EnsureSuccessStatusCode();
        (await bob.PutAsJsonAsync($"/api/v1/artifacts/{artifact.Resource.Id}", new SaveArtifactRequest("成果", "協作修改", 1))).EnsureSuccessStatusCode();
        (await alice.PutAsJsonAsync($"/api/v1/projects/{id}/access", new ResourceAclRequest([], []))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/v1/artifacts/{artifact.Resource.Id}")).StatusCode);
    }
    [Fact]
    public async Task ContextUsesProjectInstructionsAndRejectsStaleSettingsAndArchivedProjects()
    {
        await using var f = new NexusFactory(); using var client = await f.SignedInAsync();
        var p = (await (await client.PostAsJsonAsync("/api/v1/projects", new ProjectRequest("專案", Instructions: "先列結論，再提供依據"))).Content.ReadFromJsonAsync<ProjectDto>())!;
        var start = (await (await client.PostAsJsonAsync($"/api/v1/projects/{p.Resource.Id}/conversations", new ProjectConversationRequest())).Content.ReadFromJsonAsync<ProjectConversationDto>())!;
        var run = await CreateRun(client, start.Conversation.Id, "請給建議"); await WaitForTerminal(client, run.Id);
        Assert.Contains("先列結論，再提供依據", f.Provider.LastParameters!.SystemPrompt);
        (await client.PutAsJsonAsync($"/api/v1/projects/{p.Resource.Id}", new ProjectRequest("封存", ExpectedVersion: 1, IsArchived: true))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/v1/projects/{p.Resource.Id}", new ProjectRequest("覆蓋", ExpectedVersion: 1))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/v1/projects/{p.Resource.Id}/conversations", new ProjectConversationRequest())).StatusCode);
        (await client.PutAsJsonAsync($"/api/v1/conversations/{start.Conversation.Id}/project", new ConversationProjectRequest(null))).EnsureSuccessStatusCode();
    }
}
