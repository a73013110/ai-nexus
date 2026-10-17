using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiNexus.Features.Inference.Models;

/// <summary>
/// 系統核准之模型規格實體，定義模型容量、能力標籤與推論參數限制。
/// </summary>
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

    /// <summary>
    /// 設定檔中明確指定的圖片支援覆寫。null 表示交由 Ollama 即時探測決定。
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped, ConfigurationKeyName("SupportsImages")]
    public bool? ImageCapabilityOverride { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string ReasoningControl { get; set; } = "none";

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public List<string> ReasoningEfforts { get; set; } = [];

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string DefaultReasoningEffort { get; set; } = "auto";

    /// <summary>
    /// 將此模型規格綁定至特定供應商前綴，生成路由實體。
    /// </summary>
    internal ModelProfile Route(string provider)
    {
        var routed = (ModelProfile)MemberwiseClone();
        routed.Provider = provider;
        routed.ProviderModelId = Id;
        routed.Id = provider + "/" + Id;
        routed.ReasoningEfforts = [.. ReasoningEfforts];
        return routed;
    }

    /// <summary>
    /// 驗證此模型的推理/思考強度組態是否符合特定供應商的合法值。
    /// </summary>
    public bool ValidReasoning(string provider)
    {
        if (ReasoningControl == "none")
        {
            return ReasoningEfforts.Count == 0 && DefaultReasoningEffort == "auto";
        }

        string[] allowed = ReasoningControl switch
        {
            "google-level" when provider == "google" => ["minimal", "low", "medium", "high"],
            "ollama-toggle" when provider == "ollama" => ["minimal", "high"],
            "ollama-level" when provider == "ollama" => ["low", "medium", "high"],
            _ => []
        };

        return ReasoningEfforts.Count > 0
            && ReasoningEfforts.Distinct().Count() == ReasoningEfforts.Count
            && ReasoningEfforts.All(x => allowed.Contains(x))
            && (DefaultReasoningEffort == "auto" || ReasoningEfforts.Contains(DefaultReasoningEffort));
    }
}

/// <summary>模型詳細規格資料傳輸物件。</summary>
public sealed record ModelDto(
    string Id,
    string DisplayName,
    int ContextTokens,
    int MaxOutputTokens,
    bool SupportsStreaming,
    bool SupportsUsage,
    IReadOnlyList<string> ReasoningEfforts,
    string DefaultReasoningEffort,
    bool SupportsImages = false,
    string? Provider = null);

/// <summary>全域模型使用原則資料傳輸物件。</summary>
public sealed record ModelPolicyDto(
    bool AllowModelSelection,
    bool ShowModelNames,
    string? DefaultModelId,
    int MaxInputCharacters = 12000);

/// <summary>可用模型目錄與狀態整合資料傳輸物件。</summary>
public sealed record ModelsDto(
    IReadOnlyList<ModelDto> Models,
    bool ProviderAvailable,
    string? Notice,
    ModelPolicyDto Policy,
    IReadOnlyList<ProviderStatusDto>? Providers = null);

/// <summary>
/// ModelProfile 的 EF Core 資料表結構組態。
/// </summary>
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
