using AiNexus.BuildingBlocks;
using AiNexus.Modules.Conversations;
using AiNexus.Modules.Identity;
using AiNexus.Modules.Knowledge;
using Microsoft.Extensions.Options;

namespace AiNexus.Modules.Inference;

// IDs are references only. Every browser-facing label is resolved here, never from an ID.
public sealed class ModelPresentation(IOptions<InferenceOptions> options, IOptions<KnowledgeOptions> knowledge)
{
    public string? DisplayName(string? id, bool administrator = false, string? provider = null)
    {
        if (id is null) return null;
        if (id == "web-search" && (provider is null or "searxng" or "brave")) return "網路搜尋";
        var index = options.Value.Models.FindIndex(x => provider is null ? x.Id == id : x.Provider == provider && (x.Id == id || x.NativeId == id));
        if (index >= 0)
        {
            if (!administrator && !options.Value.ShowModelNames) return $"AI 助理 {index + 1}";
            var name = options.Value.Models[index].DisplayName?.Trim();
            return !string.IsNullOrEmpty(name) ? name : $"AI 助理 {index + 1}";
        }
        if (id == knowledge.Value.EmbeddingModel && (provider is null || provider == knowledge.Value.EmbeddingProvider)) return "知識向量模型";
        return "已停用的模型";
    }
    public string PublicId(string id)
    {
        if (options.Value.ShowModelNames) return id;
        var index = options.Value.Models.FindIndex(x => x.Id == id);
        if (index >= 0) return $"model-{index + 1}";
        if (id == "web-search") return "web-search";
        return id == knowledge.Value.EmbeddingModel ? "embedding-model" : "retired-model";
    }

    public string? DefaultId => options.Value.DefaultModelId ?? options.Value.Models.FirstOrDefault()?.Id;
    public string? InternalId(string id) => options.Value.Models.FirstOrDefault(x => PublicId(x.Id) == id)?.Id;
    public ModelPolicyDto Policy => new(options.Value.AllowModelSelection, options.Value.ShowModelNames, DefaultId is { } id ? PublicId(id) : null, options.Value.MaxInputCharacters);
    public ModelDto Model(ModelProfile model) => model.ToDto() with { Id = PublicId(model.Id), DisplayName = DisplayName(model.Id)!, Provider = options.Value.ShowModelNames ? model.Provider : null };
    public RunDto Run(GenerationRun run) => run.ToDto() with { ModelId = PublicId(run.ModelId), ModelDisplayName = DisplayName(run.ModelId) };
    public MessageDto Message(Message message) => message.ToDto() with { ModelId = message.ModelId is { } id ? PublicId(id) : null, ModelDisplayName = DisplayName(message.ModelId) };
    public PreferencesDto Preferences(UserPreferences value) => value.ToDto() with { DefaultModelId = options.Value.AllowModelSelection && value.DefaultModelId is { } id ? PublicId(id) : null };
}
