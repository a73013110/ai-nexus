using System.Net.Http.Json;
using System.Text;
using AiNexus.Features.Attachments;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Knowledge.Documents;
using AiNexus.Features.Projects;
using Microsoft.AspNetCore.Http;

namespace AiNexus.IntegrationTests.Support;

internal static class ProjectApi
{
    internal static async Task<ProjectDto> CreateProject(HttpClient client, string name)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/projects", new ProjectRequest(name));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProjectDto>())!;
    }

    internal static async Task<DocumentDto> AddProjectFile(HttpClient client, Guid project, string name)
    {
        using var body = new MultipartFormDataContent();
        body.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("專案參考：" + name)), "file", name);
        using var uploaded = await client.PostAsync("/api/v1/attachments", body);
        uploaded.EnsureSuccessStatusCode();
        var file = (await uploaded.Content.ReadFromJsonAsync<AttachmentDto>())!;
        using var added = await client.PostAsJsonAsync($"/api/v1/projects/{project}/files", new AddDocumentRequest(file.Id));
        added.EnsureSuccessStatusCode();
        return (await added.Content.ReadFromJsonAsync<DocumentDto>())!;
    }

    internal static async Task Share(HttpClient owner, string route, params (Guid User, string Role)[] members)
        => (await owner.PutAsJsonAsync(route, new ResourceAclRequest(members.Select(x => new ResourceMemberUpdate(x.User, x.Role)).ToArray(), []))).EnsureSuccessStatusCode();
}
