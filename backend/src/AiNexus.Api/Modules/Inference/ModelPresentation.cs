using AiNexus.BuildingBlocks;
using AiNexus.Modules.Conversations;
using AiNexus.Modules.Identity;
using Microsoft.Extensions.Options;

namespace AiNexus.Modules.Inference;

// Provider identifiers stay in SQL; browser identifiers follow the server's visibility policy.
public sealed class ModelPresentation(IOptions<InferenceOptions> options)
{
    public string PublicId(string id)
    {
        if (options.Value.ShowModelNames) return id;
        var index = options.Value.Models.FindIndex(x => x.Id == id);
        return index < 0 ? "retired-model" : $"model-{index + 1}";
    }

    public string? DefaultId => options.Value.DefaultModelId ?? options.Value.Models.FirstOrDefault()?.Id;
    public string? InternalId(string id) => options.Value.Models.FirstOrDefault(x => PublicId(x.Id) == id)?.Id;
    public ModelPolicyDto Policy => new(options.Value.AllowModelSelection, options.Value.ShowModelNames, DefaultId is { } id ? PublicId(id) : null, options.Value.MaxInputCharacters);
    public ModelDto Model(ModelProfile model) => model.ToDto() with { Id = PublicId(model.Id), DisplayName = options.Value.ShowModelNames ? model.DisplayName : "AI 助理 " + (options.Value.Models.IndexOf(model) + 1), Provider = options.Value.ShowModelNames ? model.Provider : null };
    public RunDto Run(GenerationRun run) => run.ToDto() with { ModelId = PublicId(run.ModelId) };
    public MessageDto Message(Message message) => message.ToDto() with { ModelId = message.ModelId is { } id ? PublicId(id) : null };
    public PreferencesDto Preferences(UserPreferences value) => value.ToDto() with { DefaultModelId = options.Value.AllowModelSelection && value.DefaultModelId is { } id ? PublicId(id) : null };
}
