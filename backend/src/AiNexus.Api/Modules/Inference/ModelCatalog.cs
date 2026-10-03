using System.Text.Json;
using Microsoft.Extensions.Options;

namespace AiNexus.Modules.Inference;

public sealed class ModelCatalog(IInferenceProvider provider, IOptions<InferenceOptions> options, ModelPresentation presentation)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private DateTimeOffset checkedAt;
    private IReadOnlySet<string> installed = new HashSet<string>();
    private bool available;
    private string? failureNotice;

    public async Task<AiNexus.BuildingBlocks.ModelsDto> GetAsync(CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            if (DateTimeOffset.UtcNow - checkedAt > TimeSpan.FromSeconds(10))
            {
                try { installed = await provider.InstalledModelsAsync(ct); available = true; failureNotice = null; }
                catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException or IOException or AiNexus.BuildingBlocks.ApiException)
                {
                    ct.ThrowIfCancellationRequested();
                    installed = new HashSet<string>();
                    available = false;
                    failureNotice = (exception as AiNexus.BuildingBlocks.ApiException)?.Message;
                }
                checkedAt = DateTimeOffset.UtcNow;
            }
            var allowed = options.Value.Models.Where(x => installed.Contains(x.Id) && (options.Value.AllowModelSelection || x.Id == presentation.DefaultId)).Select(presentation.Model).ToList();
            return new(allowed, available, !available ? failureNotice ?? "目前無法連線至模型服務，請稍後重試。" : allowed.Count == 0 ? "系統指定的模型尚未就緒，請由管理員確認模型設定。" : null, presentation.Policy);
        }
        finally { gate.Release(); }
    }

    public async Task<ModelProfile> RequireAsync(string? id, CancellationToken ct)
    {
        var requested = id ?? presentation.Policy.DefaultModelId;
        if (!options.Value.AllowModelSelection && requested != presentation.Policy.DefaultModelId) throw new AiNexus.BuildingBlocks.ApiException(400, "model_selection_disabled", "模型由系統指定，無法自行切換。");
        var catalog = await GetAsync(ct);
        if (!catalog.ProviderAvailable) throw new AiNexus.BuildingBlocks.ApiException(503, "provider_unavailable", catalog.Notice!);
        if (!catalog.Models.Any(x => x.Id == requested)) throw new AiNexus.BuildingBlocks.ApiException(400, "model_not_allowed", "此模型不可用或未經伺服器核准。");
        return options.Value.Models.Single(x => presentation.PublicId(x.Id) == requested);
    }

    public static string RequireReasoning(ModelProfile model, string? effort)
    {
        var value = effort ?? model.DefaultReasoningEffort;
        if (value != "auto" && !model.ReasoningEfforts.Contains(value, StringComparer.Ordinal)) throw new AiNexus.BuildingBlocks.ApiException(400, "reasoning_not_supported", "此模型不支援選擇的思考強度。");
        return value;
    }
}
