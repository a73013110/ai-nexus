namespace AiNexus.Features.Inference;

/// <summary>A configured model. Its EF mapping lives in <c>NexusDbContext</c>.</summary>
public sealed class ModelProfile
{
    public string Id { get; set; } = "";
    public string Provider { get; set; } = "google";
    public string ProviderModelId { get; set; } = "";
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string NativeId => ProviderModelId;
    public string DisplayName { get; set; } = "";
    public int ContextTokens { get; set; } = 8192;
    public int MaxOutputTokens { get; set; } = 2048;
    public bool SupportsStreaming { get; set; } = true;
    public bool SupportsUsage { get; set; } = true;
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool SupportsImages { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool? ImageCapabilityOverride { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string ReasoningControl { get; set; } = "none";
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public List<string> ReasoningEfforts { get; set; } = [];
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string DefaultReasoningEffort { get; set; } = "auto";

    public bool ValidReasoning(string provider)
    {
        if (ReasoningControl == "none") return ReasoningEfforts.Count == 0 && DefaultReasoningEffort == "auto";
        string[] allowed = ReasoningControl switch {
            "google-level" when provider == "google" => ["minimal", "low", "medium", "high"],
            "ollama-toggle" when provider == "ollama" => ["minimal", "high"],
            "ollama-level" when provider == "ollama" => ["low", "medium", "high"],
            _ => []
        };
        return ReasoningEfforts.Count > 0 && ReasoningEfforts.Distinct().Count() == ReasoningEfforts.Count && ReasoningEfforts.All(x => allowed.Contains(x)) && (DefaultReasoningEffort == "auto" || ReasoningEfforts.Contains(DefaultReasoningEffort));
    }
}

public sealed record ModelDto(string Id, string DisplayName, int ContextTokens, int MaxOutputTokens, bool SupportsStreaming, bool SupportsUsage, IReadOnlyList<string> ReasoningEfforts, string DefaultReasoningEffort, bool SupportsImages = false, string? Provider = null);
public sealed record ModelPolicyDto(bool AllowModelSelection, bool ShowModelNames, string? DefaultModelId, int MaxInputCharacters = 12000);
public sealed record ModelsDto(IReadOnlyList<ModelDto> Models, bool ProviderAvailable, string? Notice, ModelPolicyDto Policy, IReadOnlyList<AiNexus.Features.Inference.ProviderStatusDto>? Providers = null);
