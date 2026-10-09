using System.Runtime.CompilerServices;
using System.Text.Json;

namespace AiNexus.Features.Inference;

// Ollama wire format is confined to this adapter.
public sealed class OllamaProvider(HttpClient client) : IInferenceProvider
{
    public async Task<IReadOnlySet<string>> InstalledModelsAsync(CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        using var response = await client.GetAsync("api/tags", timeout.Token);
        response.EnsureSuccessStatusCode();
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(timeout.Token), cancellationToken: timeout.Token);
        return json.RootElement.GetProperty("models").EnumerateArray().Select(x => x.GetProperty("name").GetString()!).ToHashSet(StringComparer.Ordinal);
    }

    public async Task<ModelCapabilities?> CapabilitiesAsync(string model, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        using var response = await client.PostAsJsonAsync("api/show", new { model }, timeout.Token);
        response.EnsureSuccessStatusCode();
        using var json = await AiNexus.Platform.Http.BoundedHttpJson.ReadAsync(response, 4 * 1024 * 1024, timeout.Token);
        if (!json.RootElement.TryGetProperty("capabilities", out var capabilities) || capabilities.ValueKind != JsonValueKind.Array) return null;
        return new(capabilities.EnumerateArray().Any(x => x.GetString() == "vision"));
    }

    public async IAsyncEnumerable<InferenceChunk> StreamAsync(string model, IReadOnlyList<InferenceMessage> messages, GenerationParameters parameters, [EnumeratorCancellation] CancellationToken ct)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["messages"] = messages.Select(x => new { role = x.Role, content = x.Content, images = (x.Images ?? []).Select(i => Convert.ToBase64String(i.Data ?? throw new InvalidDataException("Image data missing."))).ToArray() }),
            ["stream"] = true,
            ["options"] = new { num_ctx = parameters.ContextTokens, num_predict = parameters.MaxOutputTokens, temperature = parameters.Temperature },
            ["keep_alive"] = "5m"
        };
        if (parameters.ReasoningControl == "ollama-toggle" && parameters.ReasoningEffort != "auto") payload["think"] = parameters.ReasoningEffort == "high";
        else if (parameters.ReasoningControl == "ollama-level" && parameters.ReasoningEffort != "auto") payload["think"] = parameters.ReasoningEffort;
        else if (parameters.ReasoningControl == "none") payload["think"] = false;
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/chat") { Content = JsonContent.Create(payload) };
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(ct));
        var completed = false;
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (line.Length == 0) continue;
            if (line.Length > 262144) throw new InvalidDataException("Provider frame exceeds limit.");
            using var json = JsonDocument.Parse(line);
            var root = json.RootElement;
            if (root.TryGetProperty("error", out _)) throw new InvalidDataException("Provider reported an error.");
            var text = root.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var content) ? content.GetString() ?? "" : "";
            var done = root.TryGetProperty("done", out var end) && end.GetBoolean();
            var inputTokens = root.TryGetProperty("prompt_eval_count", out var input) ? input.GetInt64() : (long?)null;
            var outputTokens = root.TryGetProperty("eval_count", out var output) ? output.GetInt64() : (long?)null;
            yield return new InferenceChunk(text, done, inputTokens, outputTokens, CachedInputTokens: done ? 0 : null);
            if (done) { completed = true; break; }
        }
        if (!completed) throw new InvalidDataException("Provider stream ended without completion.");
    }
}
