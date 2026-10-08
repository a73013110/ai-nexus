namespace AiNexus.Features.Inference;

public sealed class InferenceOptions
{
    public Dictionary<string, int> ProviderConcurrency { get; set; } = new(StringComparer.Ordinal) { ["google"] = 1 };
    public string GoogleApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "http://localhost:11434/";
    public int QueueCapacity { get; set; } = 16;
    public int TimeoutSeconds { get; set; } = 180;
    public int MaxInputCharacters { get; set; } = 12000;
    public int MaxOutputCharacters { get; set; } = 65536;
    public string SystemPrompt { get; set; } = "請用繁體中文回答。";
    public bool AllowModelSelection { get; set; } = true;
    public bool ShowModelNames { get; set; } = true;
    public string? DefaultModelId { get; set; }
    public List<ModelProfile> Models { get; set; } = [];
}

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
