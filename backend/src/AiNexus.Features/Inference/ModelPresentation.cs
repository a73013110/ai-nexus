using AiNexus.Features.Identity;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Inference;

/// <summary>
/// A model a platform service calls on its own rather than for chat, such as the knowledge embedding model. The owning
/// module registers it so reports and prices can name it.
/// </summary>
public sealed record ServiceModel(string Provider, string Id, string DisplayName, string PublicId);

// IDs are references only. Every browser-facing label is resolved here, never from an ID.
public sealed class ModelPresentation(IOptions<InferenceOptions> options, IEnumerable<ServiceModel> services)
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
        return services.FirstOrDefault(x => x.Id == id && (provider is null || provider == x.Provider))?.DisplayName ?? "已停用的模型";
    }
    public string PublicId(string id)
    {
        if (options.Value.ShowModelNames) return id;
        var index = options.Value.Models.FindIndex(x => x.Id == id);
        if (index >= 0) return $"model-{index + 1}";
        if (id == "web-search") return "web-search";
        return services.FirstOrDefault(x => x.Id == id)?.PublicId ?? "retired-model";
    }

    public string? DefaultId => options.Value.DefaultModelId ?? options.Value.Models.FirstOrDefault()?.Id;
    public string? InternalId(string id) => options.Value.Models.FirstOrDefault(x => PublicId(x.Id) == id)?.Id;
    public ModelPolicyDto Policy => new(options.Value.AllowModelSelection, options.Value.ShowModelNames, DefaultId is { } id ? PublicId(id) : null, options.Value.MaxInputCharacters);
    public ModelDto Model(ModelProfile model) => model.ToDto() with { Id = PublicId(model.Id), DisplayName = DisplayName(model.Id)!, Provider = options.Value.ShowModelNames ? model.Provider : null };
    public RunDto Run(GenerationRun run) => run.ToDto() with { ModelId = PublicId(run.ModelId), ModelDisplayName = DisplayName(run.ModelId) };
    public PreferencesDto Preferences(UserPreferences value) => value.ToDto() with { DefaultModelId = options.Value.AllowModelSelection && value.DefaultModelId is { } id ? PublicId(id) : null };
}
