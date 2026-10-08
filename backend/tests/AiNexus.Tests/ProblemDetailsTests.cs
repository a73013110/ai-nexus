using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Errors;
using AiNexus.Features.Library;
using Xunit;

namespace AiNexus.Tests;

public sealed class ProblemDetailsTests
{
    [Fact]
    public void EveryErrorKindMapsToAClientOrServerStatus()
    {
        foreach (var kind in Enum.GetValues<ErrorKind>())
            Assert.InRange(Problems.Status(kind), 400, 599);
    }

    [Fact]
    public async Task FrameworkFailuresUseTheSafeFormatWhateverTheClientAccepts()
    {
        await using var factory = new NexusFactory();
        using var anonymous = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/conversations");
        request.Headers.Accept.ParseAdd("text/html");
        using var response = await anonymous.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;
        Assert.Equal(["type", "title", "status", "code", "issueCode"], root.EnumerateObject().Select(p => p.Name));
        Assert.Equal("urn:ai-nexus:problem:authentication_required", root.GetProperty("type").GetString());
        Assert.True(Issues.ValidCode(root.GetProperty("issueCode").GetString()));
    }

    [Fact]
    public async Task UnknownApiPathsReturnProblemsInsteadOfTheApplicationShell()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync();
        using var response = await client.GetAsync("/api/v1/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("not_found", body.RootElement.GetProperty("code").GetString());
        Assert.False(body.RootElement.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task InvalidRequestsNameTheFieldsWithoutEchoingInput()
    {
        await using var factory = new NexusFactory();
        using var client = await factory.SignedInAsync();
        using var response = await client.PostAsJsonAsync("/api/v1/prompt-templates", new SavePromptRequest("   ", "fixture-secret-content"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("fixture-secret-content", text);
        using var body = JsonDocument.Parse(text);
        Assert.Equal("invalid_prompt_template", body.RootElement.GetProperty("code").GetString());
        var errors = body.RootElement.GetProperty("errors");
        Assert.Equal(["title"], errors.EnumerateObject().Select(p => p.Name));
        Assert.Equal(["length"], errors.GetProperty("title").EnumerateArray().Select(e => e.GetString()));
    }
}
