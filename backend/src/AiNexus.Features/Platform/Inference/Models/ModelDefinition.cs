namespace AiNexus.Features.Platform.Inference.Models;

/// <summary>
/// 設定檔 <c>Inference:Models</c> 核准的模型規格：容量、能力標籤與推論參數限制。
/// 啟動時同步寫入 <c>inference.ModelProfiles</c>（只保存其中的對應欄位），供用量與計費紀錄參照。
/// </summary>
public sealed class ModelDefinition
{
    public string Id { get; set; } = "";
    public string Provider { get; set; } = "google";
    public string ProviderModelId { get; set; } = "";
    public string NativeId => ProviderModelId;
    public string DisplayName { get; set; } = "";
    public int ContextTokens { get; set; } = 8192;
    public int MaxOutputTokens { get; set; } = 2048;
    public bool SupportsStreaming { get; set; } = true;
    public bool SupportsUsage { get; set; } = true;
    public bool SupportsImages { get; set; }

    /// <summary>
    /// 設定檔中明確指定的圖片支援覆寫。null 表示交由 Ollama 即時探測決定。
    /// </summary>
    [ConfigurationKeyName("SupportsImages")]
    public bool? ImageCapabilityOverride { get; set; }

    public string ReasoningControl { get; set; } = "none";
    public List<string> ReasoningEfforts { get; set; } = [];
    public string DefaultReasoningEffort { get; set; } = "auto";

    /// <summary>
    /// 將此模型規格綁定至特定供應商前綴，生成路由實體。
    /// </summary>
    internal ModelDefinition Route(string provider)
    {
        var routed = (ModelDefinition)MemberwiseClone();
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
