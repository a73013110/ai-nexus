namespace AiNexus.Features.Platform.Inference.Models;

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
