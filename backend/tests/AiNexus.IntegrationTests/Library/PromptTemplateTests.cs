using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Library;

namespace AiNexus.IntegrationTests.Library;

public sealed class PromptTemplateTests
{
    [Fact]
    public async Task PromptLibraryIsPersonalAndSupportsCrud()
    {
        await using var factory = new NexusFactory();
        using var alice = await factory.SignedInAsync();
        using var bob = await factory.SignedInAsync("bob");
        var response = await alice.PostAsJsonAsync("/api/v1/prompt-templates", new SavePromptRequest("摘要", "請歸納以下內容"));
        response.EnsureSuccessStatusCode();
        var prompt = (await response.Content.ReadFromJsonAsync<PromptTemplateDto>())!;
        Assert.Empty((await bob.GetFromJsonAsync<List<PromptTemplateDto>>("/api/v1/prompt-templates"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PutAsJsonAsync($"/api/v1/prompt-templates/{prompt.Id}", new SavePromptRequest("steal", "x"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.DeleteAsync($"/api/v1/prompt-templates/{prompt.Id}")).StatusCode);
        (await alice.PutAsJsonAsync($"/api/v1/prompt-templates/{prompt.Id}", new SavePromptRequest("新版", "updated"))).EnsureSuccessStatusCode();
        Assert.Equal("updated", Assert.Single((await alice.GetFromJsonAsync<List<PromptTemplateDto>>("/api/v1/prompt-templates"))!).Content);
        (await alice.DeleteAsync($"/api/v1/prompt-templates/{prompt.Id}")).EnsureSuccessStatusCode();
        Assert.Empty((await alice.GetFromJsonAsync<List<PromptTemplateDto>>("/api/v1/prompt-templates"))!);
    }
}
