using AiNexus.Features.Identity.Users;
using AiNexus.Features.Inference.Runs;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Inference.Models;

/// <summary>
/// 平台內部服務專用模型資訊（例如知識庫向量檢索使用的 Embedding 模型）。
/// </summary>
public sealed record ServiceModel(string Provider, string Id, string DisplayName, string PublicId);

/// <summary>
/// 模型展示與識別碼投影服務。
/// 統一負責將內部模型識別碼、供應商與名稱轉換為前端使用者介面友善的別名；
/// 當系統組態啟用名稱隱藏（ShowModelNames = false）時，自動將模型名稱遮蔽為「AI 助理 1」等泛用代稱，
/// 以達到權限防禦與外部模型隔離。
/// </summary>
public sealed class ModelPresentation(IOptions<InferenceOptions> options, IEnumerable<ServiceModel> services)
{
    /// <summary>
    /// 解析模型於前端畫面的顯示名稱。
    /// </summary>
    public string? DisplayName(string? id, bool administrator = false, string? provider = null)
    {
        if (id is null) return null;
        if (id == "web-search" && (provider is null or "searxng" or "brave")) return "網路搜尋";

        var index = options.Value.Models.FindIndex(x =>
            provider is null ? x.Id == id : x.Provider == provider && (x.Id == id || x.NativeId == id));

        if (index >= 0)
        {
            // 若非管理員且系統設定隱藏真實模型名稱，統一以「AI 助理 N」呈現
            if (!administrator && !options.Value.ShowModelNames) return $"AI 助理 {index + 1}";
            var name = options.Value.Models[index].DisplayName?.Trim();
            return !string.IsNullOrEmpty(name) ? name : $"AI 助理 {index + 1}";
        }

        return services.FirstOrDefault(x => x.Id == id && (provider is null || provider == x.Provider))?.DisplayName ?? "已停用的模型";
    }

    /// <summary>
    /// 取得對前端公開的安全模型識別碼。
    /// 當隱藏模型名稱時，將真實 ID 轉為虛擬 ID（例如 model-1）。
    /// </summary>
    public string PublicId(string id)
    {
        if (options.Value.ShowModelNames) return id;

        var index = options.Value.Models.FindIndex(x => x.Id == id);
        if (index >= 0) return $"model-{index + 1}";
        if (id == "web-search") return "web-search";

        return services.FirstOrDefault(x => x.Id == id)?.PublicId ?? "retired-model";
    }

    /// <summary>系統組態設定的預設模型識別碼。</summary>
    public string? DefaultId => options.Value.DefaultModelId ?? options.Value.Models.FirstOrDefault()?.Id;

    /// <summary>將前端傳入的公開 ID 還原為內部真實模型 ID。</summary>
    public string? InternalId(string id) => options.Value.Models.FirstOrDefault(x => PublicId(x.Id) == id)?.Id;

    /// <summary>取得前端模型互動原則（是否允許選模、最大輸入長度等）。</summary>
    public ModelPolicyDto Policy => new(
        options.Value.AllowModelSelection,
        options.Value.ShowModelNames,
        DefaultId is { } id ? PublicId(id) : null,
        options.Value.MaxInputCharacters);

    /// <summary>將模型組態轉換為前端展示 DTO。</summary>
    public ModelDto Model(ModelProfile model) => model.ToDto() with
    {
        Id = PublicId(model.Id),
        DisplayName = DisplayName(model.Id)!,
        Provider = options.Value.ShowModelNames ? model.Provider : null
    };

    /// <summary>將生成執行實體轉換為前端展示 DTO。</summary>
    public RunDto Run(GenerationRun run) => run.ToDto() with
    {
        ModelId = PublicId(run.ModelId),
        ModelDisplayName = DisplayName(run.ModelId)
    };

    /// <summary>將使用者偏好設定轉換為前端展示 DTO（包含公開的模型 ID）。</summary>
    public PreferencesDto Preferences(UserPreferences value) => value.ToDto() with
    {
        DefaultModelId = options.Value.AllowModelSelection && value.DefaultModelId is { } id ? PublicId(id) : null
    };
}
