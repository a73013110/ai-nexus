using System.Runtime.CompilerServices;
using System.Text.Json;

namespace AiNexus.Features.Inference.Providers;

/// <summary>
/// Ollama 本地模型推論轉接器。
/// 封裝 Ollama 專屬的 HTTP 通訊協定、參數規格與串流事件解析。
/// </summary>
public sealed class OllamaProvider(HttpClient client) : IInferenceProvider
{
    /// <summary>
    /// 取得目前 Ollama 實例已安裝的模型名稱清單。
    /// </summary>
    public async Task<IReadOnlySet<string>> InstalledModelsAsync(CancellationToken ct)
    {
        // 探測安裝模型使用 5 秒短超時，避免本機 Ollama 服務卡住時阻塞上層探測
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));

        using var response = await client.GetAsync("api/tags", timeout.Token);
        response.EnsureSuccessStatusCode();

        using var json = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(timeout.Token),
            cancellationToken: timeout.Token);

        return json.RootElement.GetProperty("models")
            .EnumerateArray()
            .Select(x => x.GetProperty("name").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// 查詢指定模型的功能特徵（例如是否支援視覺/圖片輸入）。
    /// </summary>
    public async Task<ModelCapabilities?> CapabilitiesAsync(string model, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));

        using var response = await client.PostAsJsonAsync("api/show", new { model }, timeout.Token);
        response.EnsureSuccessStatusCode();

        // 讀取模型規格 JSON，限制大小為 4MB 邊界防禦
        using var json = await AiNexus.Platform.Http.BoundedHttpJson.ReadAsync(response, 4 * 1024 * 1024, timeout.Token);
        if (!json.RootElement.TryGetProperty("capabilities", out var capabilities) || capabilities.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return new(capabilities.EnumerateArray().Any(x => x.GetString() == "vision"));
    }

    /// <summary>
    /// 建立與 Ollama 的串流生成交談。
    /// </summary>
    public async IAsyncEnumerable<InferenceChunk> StreamAsync(
        string model,
        IReadOnlyList<InferenceMessage> messages,
        GenerationParameters parameters,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["messages"] = messages.Select(x => new
            {
                role = x.Role,
                content = x.Content,
                images = (x.Images ?? []).Select(i => Convert.ToBase64String(i.Data ?? throw new InvalidDataException("Image data missing."))).ToArray()
            }),
            ["stream"] = true,
            ["options"] = new
            {
                num_ctx = parameters.ContextTokens,
                num_predict = parameters.MaxOutputTokens,
                temperature = parameters.Temperature
            },
            // 保留模型在記憶體 5 分鐘，避免連續對話時反覆冷啟動載入
            ["keep_alive"] = "5m"
        };

        // 根據模型組態設定思考/推理（Reasoning）控制參數
        if (parameters.ReasoningControl == "ollama-toggle" && parameters.ReasoningEffort != "auto")
        {
            payload["think"] = parameters.ReasoningEffort == "high";
        }
        else if (parameters.ReasoningControl == "ollama-level" && parameters.ReasoningEffort != "auto")
        {
            payload["think"] = parameters.ReasoningEffort;
        }
        else if (parameters.ReasoningControl == "none")
        {
            payload["think"] = false;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/chat")
        {
            Content = JsonContent.Create(payload)
        };
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(ct));
        var completed = false;

        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (line.Length == 0) continue;
            // 單行不得超過 256KB，防止惡意或異常回應耗盡伺服器記憶體
            if (line.Length > 262144) throw new InvalidDataException("Provider frame exceeds limit.");

            using var json = JsonDocument.Parse(line);
            var root = json.RootElement;
            if (root.TryGetProperty("error", out _))
            {
                throw new InvalidDataException("Provider reported an error.");
            }

            var text = root.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var content)
                ? content.GetString() ?? ""
                : "";
            var done = root.TryGetProperty("done", out var end) && end.GetBoolean();
            var inputTokens = root.TryGetProperty("prompt_eval_count", out var input) ? input.GetInt64() : (long?)null;
            var outputTokens = root.TryGetProperty("eval_count", out var output) ? output.GetInt64() : (long?)null;

            // 正規化為跨供應商一致的結束原因，便於呼叫端判斷是否因達到長度上限而截斷
            var finish = !done
                ? null
                : root.TryGetProperty("done_reason", out var reason) && reason.GetString() == "length"
                    ? "MAX_TOKENS"
                    : "STOP";

            yield return new InferenceChunk(text, done, inputTokens, outputTokens, finish, CachedInputTokens: done ? 0 : null);
            if (done)
            {
                completed = true;
                break;
            }
        }

        if (!completed)
        {
            throw new InvalidDataException("Provider stream ended without completion.");
        }
    }
}
