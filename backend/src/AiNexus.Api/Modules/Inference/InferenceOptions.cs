namespace AiNexus.Modules.Inference;

public sealed class InferenceOptions
{
    public string Provider { get; set; } = "google";
    public string GoogleApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "http://localhost:11434/";
    public int QueueCapacity { get; set; } = 16;
    public int TimeoutSeconds { get; set; } = 180;
    public int MaxInputCharacters { get; set; } = 12000;
    public string SystemPrompt { get; set; } = "請用繁體中文回答。";
    public bool AllowModelSelection { get; set; } = true;
    public bool ShowModelNames { get; set; } = true;
    public string? DefaultModelId { get; set; }
    public List<ModelProfile> Models { get; set; } = [];
}

public sealed record InferenceMessage(string Role, string Content);
public sealed record GenerationParameters(int ContextTokens, int MaxOutputTokens, double Temperature, string SystemPrompt, string ReasoningEffort = "auto", string ReasoningControl = "none");
public sealed record InferenceChunk(string Text, bool Done = false, long? InputTokens = null, long? OutputTokens = null);

public interface IInferenceProvider
{
    Task<IReadOnlySet<string>> InstalledModelsAsync(CancellationToken ct);
    IAsyncEnumerable<InferenceChunk> StreamAsync(string model, IReadOnlyList<InferenceMessage> messages, GenerationParameters parameters, CancellationToken ct);
}
