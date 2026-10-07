using AiNexus.BuildingBlocks.Diagnostics;
using System.Diagnostics;
using AiNexus.BuildingBlocks;

namespace AiNexus.Modules.Inference;

public sealed class GenerationRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string? TraceId { get; set; }
    public string? ParentSpanId { get; set; }
    public Guid OperationId { get; set; }
    // Unique filtered index enforces one active generation per owner, even across requests.
    public Guid? ActiveOwnerId { get; set; }
    public Guid? ExecutorId { get; set; }
    public DateTimeOffset? LeaseExpiresAt { get; set; }
    public Guid ConversationId { get; set; }
    public Guid UserMessageId { get; set; }
    public Guid AssistantMessageId { get; set; }
    public string ModelId { get; set; } = "";
    public string Provider { get; set; } = "google";
    public string ProviderModelId { get; set; } = "";
    public string ParametersJson { get; set; } = "{}";
    public string IdempotencyKey { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public string Status { get; set; } = RunStates.Queued;
    public string Content { get; set; } = "";
    public long LastSequence { get; set; }
    public string? IssueCode { get; set; }
    public string? ErrorCode { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public long ReservedTokens { get; set; }
    public long? InputTokens { get; set; }
    public long? OutputTokens { get; set; }
    public long? DurationMilliseconds { get; set; }
    public long? GenerationMilliseconds { get; set; }
}

public sealed class RunEvent
{
    public Guid RunId { get; set; }
    public long Sequence { get; set; }
    public string Type { get; set; } = "status";
    public string Status { get; set; } = RunStates.Queued;
    public string? Delta { get; set; }
    public string? IssueCode { get; set; }
    public string? ErrorCode { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

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
