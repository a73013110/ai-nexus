using System.Net.Http.Json;
using AiNexus.Features.Artifacts;

namespace AiNexus.IntegrationTests.Support;

internal static class ArtifactApi
{
    internal const string Content = "## 工作重點\n\n**保留事實**與必要資訊。\n\n- 第一項\n- 第二項\n\n| 項目 | 說明 |\n| --- | --- |\n| 文件 | 來源核對 |\n\n```csharp\nvar answer = 42;\n```";

    internal static async Task<ArtifactDto> CreateArtifact(HttpClient client, Guid? source = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/artifacts", new CreateArtifactRequest("工作成果", Content, source)); response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<ArtifactDto>())!;
    }
}
