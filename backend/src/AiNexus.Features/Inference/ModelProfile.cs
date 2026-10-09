using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Inference;

/// <summary>A configured model.</summary>
[Comment("核准模型的能力、上下文與輸出限制。")]
public sealed class ModelProfile
{
    [Comment("資料的主鍵識別碼。")]
    public string Id { get; set; } = "";
    [Comment("模型或搜尋服務供應商識別碼。")]
    public string Provider { get; set; } = "google";
    [Comment("送往指定供應商的原生模型識別碼，與核准路由識別碼分開保存。")]
    public string ProviderModelId { get; set; } = "";
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string NativeId => ProviderModelId;
    [Comment("模型的介面顯示名稱。")]
    public string DisplayName { get; set; } = "";
    [Comment("模型上下文容量，以 tokens 計。")]
    public int ContextTokens { get; set; } = 8192;
    [Comment("模型核准的最大輸出 tokens。")]
    public int MaxOutputTokens { get; set; } = 2048;
    [Comment("模型是否支援串流輸出。")]
    public bool SupportsStreaming { get; set; } = true;
    [Comment("模型是否會回報實際用量。")]
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

internal sealed class ModelProfileConfiguration : IEntityTypeConfiguration<ModelProfile>
{
    public void Configure(EntityTypeBuilder<ModelProfile> profile)
    {
        profile.ToTable("ModelProfiles", "inference");
        profile.HasKey(x => x.Id);
        profile.Property(x => x.Id).HasMaxLength(160);
        profile.Property(x => x.DisplayName).HasMaxLength(120);
        profile.Property(x => x.Provider).HasMaxLength(32);
        profile.Property(x => x.ProviderModelId).HasMaxLength(150);
    }
}
