using System.Text.Json;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Inference;

public sealed record ProviderStatusDto(string Id, bool Available, string? Notice);

public sealed class ModelCatalog(InferenceRouter router, IOptions<InferenceOptions> options, ModelPresentation presentation, AiNexus.Platform.Diagnostics.Issues issues)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private DateTimeOffset checkedAt;
    private IReadOnlyList<ProviderStatusDto> providers = [];
    private IReadOnlySet<string> installed = new HashSet<string>();
    private IReadOnlyDictionary<string, ModelProfile> profiles = new Dictionary<string, ModelProfile>();

    private async Task<ModelProfile> ResolveAsync(ModelProfile profile, CancellationToken ct)
    {
        var images = profile.SupportsImages;
        if (profile.Provider == "ollama" && installed.Contains(profile.Id))
        {
            try
            {
                var capabilities = await router.For(profile.Provider).CapabilitiesAsync(profile.NativeId, ct);
                images = profile.ImageCapabilityOverride != false && (capabilities?.SupportsImages ?? profile.ImageCapabilityOverride ?? false);
            }
            catch (Exception error) when (error is HttpRequestException or OperationCanceledException or JsonException or IOException or AiNexus.Platform.Errors.ApiException)
            {
                ct.ThrowIfCancellationRequested();
                issues.Report(error, "provider_capability_unavailable", LogLevel.Warning);
                images = false;
            }
        }
        return Copy(profile, images);
    }

    private static ModelProfile Copy(ModelProfile profile, bool images) => new ModelProfile
        {
            Id = profile.Id, Provider = profile.Provider, ProviderModelId = profile.ProviderModelId,
            DisplayName = profile.DisplayName, ContextTokens = profile.ContextTokens, MaxOutputTokens = profile.MaxOutputTokens,
            SupportsStreaming = profile.SupportsStreaming, SupportsUsage = profile.SupportsUsage, SupportsImages = images,
            ImageCapabilityOverride = profile.ImageCapabilityOverride, ReasoningControl = profile.ReasoningControl,
            ReasoningEfforts = profile.ReasoningEfforts, DefaultReasoningEffort = profile.DefaultReasoningEffort
        };

    private ModelProfile Current(ModelProfile profile) => Copy(profile, profile.Provider == "ollama" ? profile.ImageCapabilityOverride != false && (profiles.GetValueOrDefault(profile.Id)?.SupportsImages ?? false) : profile.SupportsImages);

    public async Task<IReadOnlyList<ModelProfile>> ProfilesAsync(CancellationToken ct)
    {
        await GetAsync(ct);
        return options.Value.Models.Select(x => Current(x)).ToArray();
    }

    public async Task<IReadOnlyList<ProviderStatusDto>> ProviderStatusesAsync(CancellationToken ct)
    {
        await GetAsync(ct);
        return providers;
    }

    private async Task<(ProviderStatusDto Status, IReadOnlySet<string> Models)> DiscoverAsync(string id, CancellationToken ct)
    {
        try { return (new(id, true, null), await router.For(id).InstalledModelsAsync(ct)); }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException or IOException or AiNexus.Platform.Errors.ApiException)
        {
            ct.ThrowIfCancellationRequested();
            var issue = issues.Report(exception, "provider_discovery_unavailable", LogLevel.Warning);
            return (new(id, false, AiNexus.Platform.Diagnostics.Issues.Message(issue)), new HashSet<string>());
        }
    }

    public async Task<AiNexus.Features.Inference.ModelsDto> GetAsync(CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            if (DateTimeOffset.UtcNow - checkedAt > TimeSpan.FromSeconds(10))
            {
                var discovered = await Task.WhenAll(options.Value.ProviderConcurrency.Keys.Select(x => DiscoverAsync(x, ct)));
                providers = discovered.Select(x => x.Status).ToArray();
                installed = options.Value.Models.Where(m => discovered.Any(p => p.Status.Id == m.Provider && p.Models.Contains(m.NativeId))).Select(m => m.Id).ToHashSet(StringComparer.Ordinal);
                profiles = (await Task.WhenAll(options.Value.Models.Select(x => ResolveAsync(x, ct)))).ToDictionary(x => x.Id, StringComparer.Ordinal);
                checkedAt = DateTimeOffset.UtcNow;
            }
            var allowed = options.Value.Models.Select(Current).Where(x => installed.Contains(x.Id) && (options.Value.AllowModelSelection || x.Id == presentation.DefaultId)).Select(presentation.Model).ToList();
            var available = providers.Any(x => x.Available);
            return new(allowed, available, !available ? providers.FirstOrDefault()?.Notice ?? "目前無法連線至模型服務，請稍後重試。" : allowed.Count == 0 ? "系統指定的模型尚未就緒，請由管理員確認模型設定。" : providers.Any(x => !x.Available) ? "部分模型供應商暫時無法使用，其餘模型可正常使用。" : null, presentation.Policy,
                options.Value.ShowModelNames ? providers : []);
        }
        finally { gate.Release(); }
    }

    public async Task<ModelProfile> RequireAsync(string? id, CancellationToken ct)
    {
        var requested = id ?? presentation.Policy.DefaultModelId;
        if (!options.Value.AllowModelSelection && requested != presentation.Policy.DefaultModelId) throw new AiNexus.Platform.Errors.ApiException(400, "model_selection_disabled", "模型由系統指定，無法自行切換。");
        var catalog = await GetAsync(ct);
        if (!catalog.ProviderAvailable) throw new AiNexus.Platform.Errors.ApiException(503, "provider_unavailable", catalog.Notice!);
        var profile = options.Value.Models.FirstOrDefault(x => presentation.PublicId(x.Id) == requested);
        if (profile is not null && providers.Any(x => x.Id == profile.Provider && !x.Available)) throw new AiNexus.Platform.Errors.ApiException(503, "provider_unavailable", "此模型供應商暫時無法使用，請選擇其他模型。");
        if (!catalog.Models.Any(x => x.Id == requested)) throw new AiNexus.Platform.Errors.ApiException(400, "model_not_allowed", "此模型不可用或未經伺服器核准。");
        return Current(options.Value.Models.Single(x => presentation.PublicId(x.Id) == requested));
    }

    public static string RequireReasoning(ModelProfile model, string? effort)
    {
        var value = effort ?? model.DefaultReasoningEffort;
        if (value != "auto" && !model.ReasoningEfforts.Contains(value, StringComparer.Ordinal)) throw new AiNexus.Platform.Errors.ApiException(400, "reasoning_not_supported", "此模型不支援選擇的思考強度。");
        return value;
    }
}
