using System.Text.Json;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Inference;

public sealed record ProviderStatusDto(string Id, bool Available, string? Notice);

/// <summary>
/// Installed models and provider availability, discovered over HTTP. Stale-while-revalidate: callers get the cached
/// result at once; when it is older than <see cref="FreshFor"/> one background refresh (single-flight) replaces it.
/// Only the first load is awaited.
/// </summary>
public sealed class ModelCatalog(InferenceRouter router, IOptions<InferenceOptions> options, ModelPresentation presentation, AiNexus.Platform.Diagnostics.Issues issues, TimeProvider? clock = null)
{
    public static readonly TimeSpan FreshFor = TimeSpan.FromSeconds(10);
    // Provider clients have no timeout of their own; a refresh that nobody cancels must still end.
    private static readonly TimeSpan DiscoveryTimeout = TimeSpan.FromSeconds(30);
    private readonly TimeProvider time = clock ?? TimeProvider.System;
    private readonly Lock gate = new();
    private Snapshot? current;
    private Task<Snapshot>? refreshing;

    private sealed record Snapshot(IReadOnlyList<ProviderStatusDto> Providers, IReadOnlySet<string> Installed, IReadOnlyDictionary<string, ModelProfile> Profiles, DateTimeOffset CheckedAt);

    private async Task<Snapshot> SnapshotAsync(CancellationToken ct)
    {
        Task<Snapshot> pending;
        lock (gate)
        {
            var cached = current;
            if (cached is not null && time.GetUtcNow() - cached.CheckedAt <= FreshFor) return cached;
            if (refreshing is null)
            {
                refreshing = pending = Task.Run(RefreshAsync, CancellationToken.None);
                // A background refresh may fail with nobody awaiting it; RefreshAsync has reported it already.
                _ = pending.ContinueWith(static x => _ = x.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            }
            else pending = refreshing;
            if (cached is not null) return cached;
        }
        // First load: wait for the shared refresh without cancelling it for the other callers.
        return await pending.WaitAsync(ct);
    }

    private async Task<Snapshot> RefreshAsync()
    {
        try
        {
            using var timeout = new CancellationTokenSource(DiscoveryTimeout);
            var discovered = await Task.WhenAll(options.Value.ProviderConcurrency.Keys.Select(x => DiscoverAsync(x, timeout.Token)));
            var installed = options.Value.Models.Where(m => discovered.Any(p => p.Status.Id == m.Provider && p.Models.Contains(m.NativeId))).Select(m => m.Id).ToHashSet(StringComparer.Ordinal);
            var profiles = (await Task.WhenAll(options.Value.Models.Select(x => ResolveAsync(x, installed, timeout.Token)))).ToDictionary(x => x.Id, StringComparer.Ordinal);
            var snapshot = new Snapshot(discovered.Select(x => x.Status).ToArray(), installed, profiles, time.GetUtcNow());
            lock (gate) { current = snapshot; refreshing = null; }
            return snapshot;
        }
        catch (Exception error)
        {
            // Keep serving the previous result; the next caller starts another refresh.
            lock (gate) refreshing = null;
            issues.Report(error, "provider_discovery_unavailable", LogLevel.Warning);
            throw;
        }
    }

    private async Task<ModelProfile> ResolveAsync(ModelProfile profile, IReadOnlySet<string> installed, CancellationToken ct)
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

    private static ModelProfile Current(ModelProfile profile, Snapshot snapshot) => Copy(profile, profile.Provider == "ollama" ? profile.ImageCapabilityOverride != false && (snapshot.Profiles.GetValueOrDefault(profile.Id)?.SupportsImages ?? false) : profile.SupportsImages);

    public async Task<IReadOnlyList<ModelProfile>> ProfilesAsync(CancellationToken ct)
    {
        var snapshot = await SnapshotAsync(ct);
        return options.Value.Models.Select(x => Current(x, snapshot)).ToArray();
    }

    public async Task<IReadOnlyList<ProviderStatusDto>> ProviderStatusesAsync(CancellationToken ct) => (await SnapshotAsync(ct)).Providers;

    private async Task<(ProviderStatusDto Status, IReadOnlySet<string> Models)> DiscoverAsync(string id, CancellationToken ct)
    {
        try { return (new(id, true, null), await router.For(id).InstalledModelsAsync(ct)); }
        // The token is the refresh's own timeout: a provider that does not answer in time is unavailable.
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException or IOException or AiNexus.Platform.Errors.ApiException)
        {
            var issue = issues.Report(exception, "provider_discovery_unavailable", LogLevel.Warning);
            return (new(id, false, AiNexus.Platform.Diagnostics.Issues.Message(issue)), new HashSet<string>());
        }
    }

    public async Task<AiNexus.Features.Inference.ModelsDto> GetAsync(CancellationToken ct) => Describe(await SnapshotAsync(ct));

    private ModelsDto Describe(Snapshot snapshot)
    {
        var providers = snapshot.Providers;
        var allowed = options.Value.Models.Select(x => Current(x, snapshot)).Where(x => snapshot.Installed.Contains(x.Id) && (options.Value.AllowModelSelection || x.Id == presentation.DefaultId)).Select(presentation.Model).ToList();
        var available = providers.Any(x => x.Available);
        return new(allowed, available, !available ? providers.FirstOrDefault()?.Notice ?? "目前無法連線至模型服務，請稍後重試。" : allowed.Count == 0 ? "系統指定的模型尚未就緒，請由管理員確認模型設定。" : providers.Any(x => !x.Available) ? "部分模型供應商暫時無法使用，其餘模型可正常使用。" : null, presentation.Policy,
            options.Value.ShowModelNames ? providers : []);
    }

    public async Task<ModelProfile> RequireAsync(string? id, CancellationToken ct)
    {
        var requested = id ?? presentation.Policy.DefaultModelId;
        if (!options.Value.AllowModelSelection && requested != presentation.Policy.DefaultModelId) throw new AiNexus.Platform.Errors.ApiException(400, "model_selection_disabled", "模型由系統指定，無法自行切換。");
        var snapshot = await SnapshotAsync(ct);
        var catalog = Describe(snapshot);
        if (!catalog.ProviderAvailable) throw new AiNexus.Platform.Errors.ApiException(503, "provider_unavailable", catalog.Notice!);
        var profile = options.Value.Models.FirstOrDefault(x => presentation.PublicId(x.Id) == requested);
        if (profile is not null && snapshot.Providers.Any(x => x.Id == profile.Provider && !x.Available)) throw new AiNexus.Platform.Errors.ApiException(503, "provider_unavailable", "此模型供應商暫時無法使用，請選擇其他模型。");
        if (!catalog.Models.Any(x => x.Id == requested)) throw new AiNexus.Platform.Errors.ApiException(400, "model_not_allowed", "此模型不可用或未經伺服器核准。");
        return Current(options.Value.Models.Single(x => presentation.PublicId(x.Id) == requested), snapshot);
    }

    public static string RequireReasoning(ModelProfile model, string? effort)
    {
        var value = effort ?? model.DefaultReasoningEffort;
        if (value != "auto" && !model.ReasoningEfforts.Contains(value, StringComparer.Ordinal)) throw new AiNexus.Platform.Errors.ApiException(400, "reasoning_not_supported", "此模型不支援選擇的思考強度。");
        return value;
    }
}
