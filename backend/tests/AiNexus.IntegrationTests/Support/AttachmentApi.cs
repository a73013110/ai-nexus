using System.Net.Http.Json;
using System.Text;
using AiNexus.Features.Attachments;
using AiNexus.Features.Inference;
using static AiNexus.IntegrationTests.Support.ChatApi;

namespace AiNexus.IntegrationTests.Support;

internal static class AttachmentApi
{
    internal static async Task<HttpResponseMessage> UploadResponse(HttpClient client, string name, byte[] bytes)
    {
        using var body = new MultipartFormDataContent();
        body.Add(new ByteArrayContent(bytes), "file", name);
        return await client.PostAsync("/api/v1/attachments", body);
    }

    internal static async Task<AttachmentDto> Upload(HttpClient client, string name, byte[] bytes)
    {
        using var response = await UploadResponse(client, name, bytes);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AttachmentDto>())!;
    }

    internal static Task<AttachmentDto> Upload(HttpClient client, string name, string text) => Upload(client, name, Encoding.UTF8.GetBytes(text));

    internal static async Task<RunDto> RunWithAttachment(HttpClient client, Guid conversation, Guid file, Guid? regenerate = null)
    {
        using var response = await PostRun(client, new(conversation, "test-model", regenerate is null ? "請分析文件" : null, null, regenerate, AttachmentIds: regenerate is null ? [file] : null));
        response.EnsureSuccessStatusCode();
        return await WaitForTerminal(client, (await response.Content.ReadFromJsonAsync<RunDto>())!.Id);
    }
}
