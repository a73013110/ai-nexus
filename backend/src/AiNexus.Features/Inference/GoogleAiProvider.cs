using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using AiNexus.Platform.Errors;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Inference;

// Native Gemini API adapter. API keys go only in the backend request header.
public sealed class GoogleAiProvider(HttpClient client, IOptions<InferenceOptions> options) : IInferenceProvider
{
    private HttpRequestMessage Request(HttpMethod method, string path)
    {
        var key = options.Value.GoogleApiKey;
        if (string.IsNullOrWhiteSpace(key)) throw new ApiException(503, "google_api_key_missing", "尚未設定後端 Google AI API key。");
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("x-goog-api-key", key);
        return request;
    }

    public async Task<IReadOnlySet<string>> InstalledModelsAsync(CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        var models = new HashSet<string>(StringComparer.Ordinal);
        string? token = null;
        for (var page = 0; page < 10; page++)
        {
            using var request = Request(HttpMethod.Get, "models?pageSize=1000" + (token is null ? "" : "&pageToken=" + Uri.EscapeDataString(token)));
            using var response = await client.SendAsync(request, timeout.Token);
            RequireSuccess(response);
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(timeout.Token), cancellationToken: timeout.Token);
            var root = json.RootElement;
            if (root.TryGetProperty("models", out var rows)) foreach (var row in rows.EnumerateArray())
            {
                if (!row.TryGetProperty("supportedGenerationMethods", out var methods) || !methods.EnumerateArray().Any(x => x.GetString() == "generateContent")) continue;
                var name = row.GetProperty("name").GetString();
                if (name?.StartsWith("models/", StringComparison.Ordinal) == true) models.Add(name[7..]);
            }
            token = root.TryGetProperty("nextPageToken", out var next) ? next.GetString() : null;
            if (string.IsNullOrEmpty(token)) return models;
        }
        throw new InvalidDataException("Model catalog exceeds page limit.");
    }

    public async IAsyncEnumerable<InferenceChunk> StreamAsync(string model, IReadOnlyList<InferenceMessage> messages, GenerationParameters parameters, [EnumeratorCancellation] CancellationToken ct)
    {
        using var request = Request(HttpMethod.Post, $"models/{Uri.EscapeDataString(model)}:streamGenerateContent?alt=sse");
        var generation = new Dictionary<string, object?> { ["temperature"] = parameters.Temperature, ["maxOutputTokens"] = parameters.MaxOutputTokens, ["candidateCount"] = 1 };
        if (parameters.ReasoningControl == "google-level" && parameters.ReasoningEffort != "auto") generation["thinkingConfig"] = new { thinkingLevel = parameters.ReasoningEffort };
        request.Content = JsonContent.Create(new
        {
            contents = messages.Where(x => x.Role != "system").Select(x => new { role = x.Role == "assistant" ? "model" : "user", parts = Parts(x) }),
            systemInstruction = new { parts = new[] { new { text = parameters.SystemPrompt } } },
            generationConfig = generation
        });
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        RequireSuccess(response);
        if (response.Content.Headers.ContentType?.MediaType != "text/event-stream") throw new ApiException(502, "provider_protocol_error", "模型服務未回傳預期的串流格式。");
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(ct));
        var frame = new StringBuilder();
        var completed = false;
        string? finishReason = null;
        long? input = null, output = null, cached = null, reasoning = null;
        while (true)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null || line.Length == 0)
            {
                if (frame.Length > 0)
                {
                    using var json = JsonDocument.Parse(frame.ToString());
                    frame.Clear();
                    var root = json.RootElement;
                    if (root.TryGetProperty("error", out _)) throw new ApiException(502, "google_stream_error", "Google AI 串流失敗，請重新生成。");
                    if (root.TryGetProperty("promptFeedback", out var feedback) && feedback.TryGetProperty("blockReason", out _)) throw Blocked();
                    if (root.TryGetProperty("usageMetadata", out var usage))
                    {
                        if (usage.TryGetProperty("promptTokenCount", out var prompt)) input = prompt.GetInt64();
                        cached = usage.TryGetProperty("cachedContentTokenCount", out var cache) ? cache.GetInt64() : 0;
                        reasoning = usage.TryGetProperty("thoughtsTokenCount", out var thoughts) ? thoughts.GetInt64() : 0;
                        if (usage.TryGetProperty("candidatesTokenCount", out var answer)) output = checked(answer.GetInt64() + reasoning.Value);
                    }
                    var text = new StringBuilder();
                    if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                    {
                        var candidate = candidates[0];
                        if (candidate.TryGetProperty("content", out var content) && content.TryGetProperty("parts", out var parts)) foreach (var part in parts.EnumerateArray())
                            if (!(part.TryGetProperty("thought", out var thought) && thought.GetBoolean()) && part.TryGetProperty("text", out var value)) text.Append(value.GetString());
                        if (candidate.TryGetProperty("finishReason", out var finish))
                        {
                            if (finish.GetString() is not ("STOP" or "MAX_TOKENS")) throw Blocked();
                            finishReason = finish.GetString();
                            completed = true;
                        }
                    }
                    if (text.Length > 0 || root.TryGetProperty("usageMetadata", out _)) yield return new InferenceChunk(text.ToString(), false, input, output, null, cached, reasoning);
                }
                if (line is null) break;
                continue;
            }
            if (line.Length > 262144 || frame.Length + line.Length > 262144) throw new ApiException(502, "provider_frame_too_large", "模型回傳的單筆串流資料超過限制。");
            if (line.StartsWith("data:", StringComparison.Ordinal)) frame.AppendLine(line[5..].TrimStart(' '));
        }
        if (!completed) throw new ApiException(502, "provider_stream_incomplete", "模型串流提前中斷，已保留收到的內容。");
        yield return new InferenceChunk("", true, input, output, finishReason, cached, reasoning);
    }

    private static IReadOnlyList<object> Parts(InferenceMessage message)
    {
        var parts = new List<object>();
        foreach (var image in message.Images ?? []) parts.Add(new { inlineData = new { mimeType = image.ContentType, data = Convert.ToBase64String(image.Data ?? throw new InvalidDataException("Image data missing.")) } });
        parts.Add(new { text = message.Content });
        return parts;
    }
    private static ApiException Blocked() => new(422, "google_response_blocked", "Google AI 未完成此回答，請調整提問後重試。");
    private static void RequireSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        throw response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new ApiException(503, "google_key_rejected", "Google AI API key 權限無效，請確認後端設定。"),
            HttpStatusCode.TooManyRequests => new ApiException(429, "google_quota_exceeded", "Google AI 額度或速率受限，請稍後重試。"),
            HttpStatusCode.NotFound => new ApiException(503, "google_model_unavailable", "此 Google AI 模型目前無法使用。"),
            _ => new ApiException(503, "google_service_unavailable", "Google AI 服務暫時無法使用，請稍後重試。")
        };
    }
}
