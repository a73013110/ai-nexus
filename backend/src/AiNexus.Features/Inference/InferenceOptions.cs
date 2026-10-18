using System.ComponentModel.DataAnnotations;
using AiNexus.Features.Inference.Models;
using AiNexus.Platform.Configuration;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Inference;

public sealed class InferenceOptions : IValidatableObject
{
    public const string Section = "Inference";

    [Range(1, 64)] public int QueueCapacity { get; set; } = 16;
    [Range(5, 600)] public int TimeoutSeconds { get; set; } = 180;
    [Range(100, 32000)] public int MaxInputCharacters { get; set; } = 12000;
    [Range(4096, 262144)] public int MaxOutputCharacters { get; set; } = 65536;
    [Required] public string SystemPrompt { get; set; } = "請用繁體中文回答。";
    public bool AllowModelSelection { get; set; } = true;
    public bool ShowModelNames { get; set; } = true;

    /// <summary>預先選取之模型路由識別碼（格式為 <c>provider/native-id</c>）；為 null 時自動挑選第一個已核准模型。</summary>
    public string? DefaultModelId { get; set; }

    [ValidateObjectMembers] public InferenceProviders Providers { get; set; } = new();

    /// <summary>已啟用的供應商所提供之經核准模型清單，路由識別碼格式為 <c>provider/native-id</c>；於組態繫結後自 <see cref="Providers"/> 推導而得。</summary>
    public List<ModelDefinition> Models { get; set; } = [];

    /// <summary>每個已啟用供應商的並行呼叫數限制；於組態繫結後自 <see cref="Providers"/> 推導而得。</summary>
    public Dictionary<string, int> ProviderConcurrency { get; set; } = new(StringComparer.Ordinal) { ["google"] = 1 };

    /// <summary>將已啟用的供應商攤平為 <see cref="Models"/> 與 <see cref="ProviderConcurrency"/>；每個模型均保留其原生識別碼供供應商介面介接。</summary>
    internal void RouteProviders()
    {
        Models = [];
        ProviderConcurrency = new(StringComparer.Ordinal);
        foreach (var (id, provider) in new (string, ProviderOptions)[] { ("google", Providers.Google), ("ollama", Providers.Ollama) })
        {
            if (!provider.Enabled) continue;
            ProviderConcurrency.Add(id, provider.MaxConcurrency);
            Models.AddRange(provider.Models.Values.Select(model => model.Route(id)));
        }
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ProviderConcurrency.Count == 0 || !ProviderConcurrency.All(p => p.Key is "google" or "ollama" && p.Value is >= 1 and <= 8))
            yield return new("至少需啟用一個推論供應商（Providers:Google 或 Providers:Ollama）且 MaxConcurrency 介於 1 至 8。", [nameof(Providers)]);
        if (Models.Count == 0) yield return new("啟用的供應商至少需包含一個可用模型。", [nameof(Models)]);
        if (Models.Select(m => m.Id).Distinct(StringComparer.Ordinal).Count() != Models.Count) yield return new("每個供應商下的模型識別碼必須唯一。", [nameof(Models)]);
        if (DefaultModelId is not null && !Models.Any(m => m.Id == DefaultModelId))
            yield return new($"{nameof(DefaultModelId)} 必須是已啟用模型的 provider/native-id。", [nameof(DefaultModelId)]);
        foreach (var m in Models)
        {
            if (string.IsNullOrWhiteSpace(m.Id) || m.Id.Length > 160 || m.NativeId.Length is 0 or > 150
                || !m.NativeId.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or ':' or '.' or '/'))
                yield return new($"模型 '{m.Id}'：Id 必須為 1–150 個 ASCII 英文字母、數字、'-'、'_'、':'、'.' 或 '/'。", [nameof(Models)]);
            if (m.ContextTokens is < 1024 or > 32768 || m.MaxOutputTokens < 128 || m.MaxOutputTokens >= m.ContextTokens)
                yield return new($"模型 '{m.Id}'：ContextTokens 必須介於 1024–32768，且 MaxOutputTokens 介於 128 至 ContextTokens 之間。", [nameof(Models)]);
            if (!m.SupportsStreaming) yield return new($"模型 '{m.Id}'：SupportsStreaming 必須為 true。", [nameof(Models)]);
            if (!ProviderConcurrency.ContainsKey(m.Provider) || !m.ValidReasoning(m.Provider))
                yield return new($"模型 '{m.Id}'：ReasoningControl、ReasoningEfforts 與 DefaultReasoningEffort 設定不符合供應商規範。", [nameof(Models)]);
        }
    }
}

public sealed class InferenceProviders
{
    [ValidateObjectMembers] public GoogleProviderOptions Google { get; set; } = new();
    [ValidateObjectMembers] public OllamaProviderOptions Ollama { get; set; } = new();
}

public abstract class ProviderOptions
{
    public bool Enabled { get; set; }
    [Range(1, 8)] public int MaxConcurrency { get; set; } = 1;

    /// <summary>經核准的模型別名字典（例如 <c>default</c>、<c>secondary</c>）；各組態來源依別名進行合併覆寫。</summary>
    public Dictionary<string, ModelDefinition> Models { get; set; } = new(StringComparer.Ordinal);
}

public sealed class GoogleProviderOptions : ProviderOptions
{
    /// <summary>應配置於機密設定檔中；端點固定指向 Google HTTPS API。</summary>
    public string ApiKey { get; set; } = "";
}

public sealed class OllamaProviderOptions : ProviderOptions
{
    [Required, HttpEndpoint] public string Endpoint { get; set; } = "http://localhost:11434/";
}

[OptionsValidator]
public sealed partial class InferenceOptionsValidator : IValidateOptions<InferenceOptions>;

public sealed record InferenceImage(Guid AttachmentId, string ContentType, byte[]? Data, int EstimatedTokens);
public sealed record InferenceMessage(string Role, string Content, IReadOnlyList<InferenceImage>? Images = null);
public sealed record GenerationParameters(int ContextTokens, int MaxOutputTokens, double Temperature, string SystemPrompt, string ReasoningEffort = "auto", string ReasoningControl = "none", bool SupportsImages = false);
public sealed record InferenceChunk(string Text, bool Done = false, long? InputTokens = null, long? OutputTokens = null, string? FinishReason = null, long? CachedInputTokens = null, long? ReasoningTokens = null);
public sealed record ModelCapabilities(bool SupportsImages);

public interface IInferenceProvider
{
    Task<IReadOnlySet<string>> InstalledModelsAsync(CancellationToken ct);
    Task<ModelCapabilities?> CapabilitiesAsync(string model, CancellationToken ct) => Task.FromResult<ModelCapabilities?>(null);
    IAsyncEnumerable<InferenceChunk> StreamAsync(string model, IReadOnlyList<InferenceMessage> messages, GenerationParameters parameters, CancellationToken ct);
}
