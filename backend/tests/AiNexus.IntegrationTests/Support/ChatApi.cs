using System.Net.Http.Json;
using AiNexus.Features.Chat;
using AiNexus.Features.Conversations;
using AiNexus.Features.Inference;

namespace AiNexus.IntegrationTests.Support;

internal static class ChatApi
{
    internal static async Task<RunDto> SubmitRun(HttpClient client, CreateRunRequest body, string key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/runs") { Content = JsonContent.Create(body) }; request.Headers.Add("Idempotency-Key", key);
        var response = await client.SendAsync(request); response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<RunDto>())!;
    }

    internal static async Task WaitForCompletion(HttpClient client, Guid id)
    {
        Assert.Equal(RunStates.Completed, (await ChatApi.WaitForTerminal(client, id)).Status);
    }

    internal static async Task<ConversationDto> CreateConversation(HttpClient client, string? title = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/conversations", new CreateConversationRequest(title));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ConversationDto>())!;
    }

    internal static async Task<HttpResponseMessage> PostRun(HttpClient client, CreateRunRequest request, string? key = null)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/runs") { Content = JsonContent.Create(request) };
        message.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        return await client.SendAsync(message);
    }

    internal static async Task<RunDto> CreateRun(HttpClient client, Guid conversation, string prompt, Guid? parent = null)
    {
        var response = await PostRun(client, new(conversation, "test-model", prompt, parent, null));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RunDto>())!;
    }

    /// <summary>Reads the run's event stream until the first streamed text has been saved and sent.</summary>
    internal static async Task WaitForFirstDelta(HttpClient client, Guid id)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var events = await client.GetAsync($"/api/v1/runs/{id}/events", HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        events.EnsureSuccessStatusCode();
        using var reader = new StreamReader(await events.Content.ReadAsStreamAsync(timeout.Token));
        while (await reader.ReadLineAsync(timeout.Token) is { } line)
            if (line.StartsWith("data: ", StringComparison.Ordinal) && line.Contains("\"type\":\"delta\"", StringComparison.Ordinal)) return;
        Assert.Fail($"Run {id} ended without streaming any text.");
    }

    /// <summary>Follows the run's event stream, which the server ends once the run is terminal and every event is sent.</summary>
    internal static async Task<RunDto> WaitForTerminal(HttpClient client, Guid id)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using (var events = await client.GetAsync($"/api/v1/runs/{id}/events", HttpCompletionOption.ResponseHeadersRead, timeout.Token))
        {
            events.EnsureSuccessStatusCode();
            await (await events.Content.ReadAsStreamAsync(timeout.Token)).CopyToAsync(Stream.Null, timeout.Token);
        }
        var run = (await client.GetFromJsonAsync<RunDto>($"/api/v1/runs/{id}", timeout.Token))!;
        Assert.False(RunStates.IsActive(run.Status), $"Run {id} is still {run.Status} after its event stream ended.");
        return run;
    }
}
