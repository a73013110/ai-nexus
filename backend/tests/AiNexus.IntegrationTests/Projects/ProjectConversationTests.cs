using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Projects;
using static AiNexus.IntegrationTests.Support.ChatApi;

namespace AiNexus.IntegrationTests.Projects;

public sealed class ProjectConversationTests
{
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
