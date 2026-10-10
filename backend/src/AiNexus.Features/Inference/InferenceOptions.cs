using System.ComponentModel.DataAnnotations;
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
    /// <summary>Routed id (<c>provider/native-id</c>) of the preselected model; null picks the first approved model.</summary>
    public string? DefaultModelId { get; set; }
    [ValidateObjectMembers] public InferenceProviders Providers { get; set; } = new();

    /// <summary>Approved models of the enabled providers, routed as <c>provider/native-id</c>; derived from <see cref="Providers"/> after binding.</summary>
    public List<ModelProfile> Models { get; set; } = [];
    /// <summary>Concurrent calls per enabled provider; derived from <see cref="Providers"/> after binding.</summary>
    public Dictionary<string, int> ProviderConcurrency { get; set; } = new(StringComparer.Ordinal) { ["google"] = 1 };

    /// <summary>Flattens the enabled providers into <see cref="Models"/> and <see cref="ProviderConcurrency"/>; each model keeps its native id for the adapter.</summary>
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
            yield return new("Enable at least one provider (Providers:Google or Providers:Ollama) with MaxConcurrency 1–8.", [nameof(Providers)]);
        if (Models.Count == 0) yield return new("The enabled providers need at least one model.", [nameof(Models)]);
        if (Models.Select(m => m.Id).Distinct(StringComparer.Ordinal).Count() != Models.Count) yield return new("Model ids must be unique per provider.", [nameof(Models)]);
        if (DefaultModelId is not null && !Models.Any(m => m.Id == DefaultModelId))
            yield return new($"{nameof(DefaultModelId)} must be the provider/native-id of an enabled model.", [nameof(DefaultModelId)]);
        foreach (var m in Models)
        {
            if (string.IsNullOrWhiteSpace(m.Id) || m.Id.Length > 160 || m.NativeId.Length is 0 or > 150
                || !m.NativeId.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or ':' or '.' or '/'))
                yield return new($"Model '{m.Id}': Id must be 1–150 ASCII letters, digits, '-', '_', ':', '.' or '/'.", [nameof(Models)]);
            if (m.ContextTokens is < 1024 or > 32768 || m.MaxOutputTokens < 128 || m.MaxOutputTokens >= m.ContextTokens)
                yield return new($"Model '{m.Id}': ContextTokens must be 1024–32768 and MaxOutputTokens 128 to below ContextTokens.", [nameof(Models)]);
            if (!m.SupportsStreaming) yield return new($"Model '{m.Id}': SupportsStreaming must be true.", [nameof(Models)]);
            if (!ProviderConcurrency.ContainsKey(m.Provider) || !m.ValidReasoning(m.Provider))
                yield return new($"Model '{m.Id}': ReasoningControl, ReasoningEfforts and DefaultReasoningEffort do not match the provider.", [nameof(Models)]);
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
    /// <summary>Approved models by a stable alias (<c>default</c>, <c>secondary</c>); configuration sources merge by alias.</summary>
    public Dictionary<string, ModelProfile> Models { get; set; } = new(StringComparer.Ordinal);
}

public sealed class GoogleProviderOptions : ProviderOptions
{
    /// <summary>Belongs in the secrets file; the endpoint is fixed to Google's HTTPS API.</summary>
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
