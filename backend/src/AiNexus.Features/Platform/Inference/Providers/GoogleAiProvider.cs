using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AiNexus.Platform.Errors;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Platform.Inference.Providers;

/// <summary>
/// Google Gemini 原生 API 推論供應商轉接器。
/// API 金鑰僅傳遞於後端至 Google 伺服器的請求標頭中，絕不暴露至前端或用戶端。
/// </summary>
public sealed class GoogleAiProvider(HttpClient client, IOptions<InferenceOptions> options) : IInferenceProvider
{
    private HttpRequestMessage Request(HttpMethod method, string path)
    {
        var key = options.Value.Providers.Google.ApiKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ExternalServiceException(InferenceErrors.GoogleApiKeyMissing, "尚未設定後端 Google AI API key。");
        }

        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("x-goog-api-key", key);
        return request;
    }

    /// <summary>
    /// 查詢 Google AI 目前支援 generateContent 的模型清單。
    /// </summary>
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
            if (root.TryGetProperty("models", out var rows))
            {
                foreach (var row in rows.EnumerateArray())
                {
                    if (!row.TryGetProperty("supportedGenerationMethods", out var methods) || !methods.EnumerateArray().Any(x => x.GetString() == "generateContent"))
                    {
                        continue;
                    }

                    var name = row.GetProperty("name").GetString();
                    if (name?.StartsWith("models/", StringComparison.Ordinal) == true)
                    {
                        models.Add(name[7..]);
                    }
                }
            }

            token = root.TryGetProperty("nextPageToken", out var next) ? next.GetString() : null;
            if (string.IsNullOrEmpty(token))
            {
                return models;
            }
        }
        throw new InvalidDataException("Google AI 模型目錄超出分頁上限。");
    }

    /// <summary>
    /// 以 Server-Sent Events (SSE) 方式即時串流 Google AI 的生成回答。
    /// </summary>
    public async IAsyncEnumerable<InferenceChunk> StreamAsync(string model, IReadOnlyList<InferenceMessage> messages, GenerationParameters parameters, [EnumeratorCancellation] CancellationToken ct)
    {
        using var request = Request(HttpMethod.Post, $"models/{Uri.EscapeDataString(model)}:streamGenerateContent?alt=sse");
        var generation = new Dictionary<string, object?> { ["temperature"] = parameters.Temperature, ["maxOutputTokens"] = parameters.MaxOutputTokens, ["candidateCount"] = 1 };
        if (parameters.ReasoningControl == "google-level" && parameters.ReasoningEffort != "auto")
        {
            generation["thinkingConfig"] = new { thinkingLevel = parameters.ReasoningEffort };
        }

        var body = new Dictionary<string, object?>
        {
            ["contents"] = Contents(messages),
            ["systemInstruction"] = new { parts = new[] { new { text = parameters.SystemPrompt } } },
            ["generationConfig"] = generation
        };
        if (parameters.Tools is { Count: > 0 } tools)
        {
            body["tools"] = new[] { new { functionDeclarations = tools.Select(x => new { name = x.Name, description = x.Description, parametersJsonSchema = x.Parameters }) } };
        }
        request.Content = JsonContent.Create(body);

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        RequireSuccess(response);
        if (response.Content.Headers.ContentType?.MediaType != "text/event-stream")
        {
            throw new ExternalServiceException(InferenceErrors.ProviderProtocolError, "模型服務未回傳預期的串流格式。");
        }

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
                    if (root.TryGetProperty("error", out _))
                    {
                        throw new ExternalServiceException(InferenceErrors.GoogleStreamError, "Google AI 串流失敗，請重新生成。");
                    }
                    if (root.TryGetProperty("promptFeedback", out var feedback) && feedback.TryGetProperty("blockReason", out _))
                    {
                        throw Blocked();
                    }

                    var hasUsage = root.TryGetProperty("usageMetadata", out var usage);
                    if (hasUsage)
                    {
                        if (usage.TryGetProperty("promptTokenCount", out var prompt)) input = prompt.GetInt64();
                        cached = usage.TryGetProperty("cachedContentTokenCount", out var cache) ? cache.GetInt64() : 0;
                        reasoning = usage.TryGetProperty("thoughtsTokenCount", out var thoughts) ? thoughts.GetInt64() : 0;
                        if (usage.TryGetProperty("candidatesTokenCount", out var answer)) output = checked(answer.GetInt64() + reasoning.Value);
                    }

                    var text = new StringBuilder();
                    List<InferenceToolCall>? calls = null;
                    if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                    {
                        var candidate = candidates[0];
                        if (candidate.TryGetProperty("content", out var content) && content.TryGetProperty("parts", out var parts))
                        {
                            foreach (var part in parts.EnumerateArray())
                            {
                                if (part.TryGetProperty("functionCall", out var call))
                                {
                                    var arguments = call.TryGetProperty("args", out var args) && args.ValueKind == JsonValueKind.Object
                                        ? JsonNode.Parse(args.GetRawText())!.AsObject()
                                        : [];
                                    var signature = part.TryGetProperty("thoughtSignature", out var mark) ? mark.GetString() : null;
                                    (calls ??= []).Add(new(call.GetProperty("name").GetString() ?? "", arguments, signature));
                                }
                                else if (!(part.TryGetProperty("thought", out var thought) && thought.GetBoolean()) && part.TryGetProperty("text", out var value))
                                {
                                    text.Append(value.GetString());
                                }
                            }
                        }

                        if (candidate.TryGetProperty("finishReason", out var finish))
                        {
                            finishReason = finish.GetString();
                            if (finishReason is not ("STOP" or "MAX_TOKENS"))
                            {
                                throw Blocked();
                            }
                            completed = true;
                        }
                    }

                    if (text.Length > 0 || hasUsage || calls is not null)
                    {
                        yield return new InferenceChunk(text.ToString(), false, input, output, null, cached, reasoning, calls);
                    }
                }

                if (line is null) break;
                continue;
            }

            if (line.Length > 262144 || frame.Length + line.Length > 262144)
            {
                throw new ExternalServiceException(InferenceErrors.ProviderFrameTooLarge, "模型回傳的單筆串流資料超過限制。");
            }

            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                frame.AppendLine(line[5..].TrimStart(' '));
            }
        }

        if (!completed)
        {
            throw new ExternalServiceException(InferenceErrors.ProviderStreamIncomplete, "模型串流提前中斷，已保留收到的內容。");
        }
        yield return new InferenceChunk("", true, input, output, finishReason, cached, reasoning);
    }

    /// <summary>
    /// Gemini 的 contents：system 另放，assistant 是 model；工具結果是 user 的 functionResponse，
    /// 同一輪呼叫多個工具時結果要併成一則（與模型那一則的 functionCall 一一對應）。
    /// </summary>
    private static List<object> Contents(IReadOnlyList<InferenceMessage> messages)
    {
        var contents = new List<object>();
        List<object>? responses = null;
        foreach (var message in messages.Where(x => x.Role != "system"))
        {
            if (message.Role == "tool")
            {
                if (responses is null) contents.Add(new { role = "user", parts = responses = [] });
                responses.Add(new { functionResponse = new { name = message.ToolName, response = new { content = message.Content } } });
                continue;
            }
            responses = null;
            contents.Add(new { role = message.Role == "assistant" ? "model" : "user", parts = Parts(message) });
        }
        return contents;
    }

    private static IReadOnlyList<object> Parts(InferenceMessage message)
    {
        if (message.ToolCalls is { Count: > 0 } calls)
        {
            List<object> requested = message.Content.Length > 0 ? [new { text = message.Content }] : [];
            requested.AddRange(calls.Select(x => x.Signature is { } signature
                ? (object)new { functionCall = new { name = x.Name, args = x.Arguments }, thoughtSignature = signature }
                : new { functionCall = new { name = x.Name, args = x.Arguments } }));
            return requested;
        }

        var parts = new List<object>();
        foreach (var image in message.Images ?? [])
        {
            parts.Add(new
            {
                inlineData = new
                {
                    mimeType = image.ContentType,
                    data = Convert.ToBase64String(image.Data ?? throw new InvalidDataException("缺少圖片二進位資料。"))
                }
            });
        }
        parts.Add(new { text = message.Content });
        return parts;
    }

    private static ExternalServiceException Blocked() => new(InferenceErrors.GoogleResponseBlocked, "Google AI 未完成此回答，請調整提問後重試。");

    private static void RequireSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        throw response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new ExternalServiceException(InferenceErrors.GoogleKeyRejected, "Google AI API key 權限無效，請確認後端設定。"),
            HttpStatusCode.TooManyRequests => new ExternalServiceException(InferenceErrors.GoogleQuotaExceeded, "Google AI 額度或速率受限，請稍後重試。"),
            HttpStatusCode.NotFound => new ExternalServiceException(InferenceErrors.GoogleModelUnavailable, "此 Google AI 模型目前無法使用。"),
            _ => new ExternalServiceException(InferenceErrors.GoogleServiceUnavailable, "Google AI 服務暫時無法使用，請稍後重試。")
        };
    }
}
