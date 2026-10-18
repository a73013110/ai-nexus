using System.Text.Json;
using AiNexus.Features.Inference.Providers;
using AiNexus.Platform.Errors;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Inference.Models;

/// <summary>
/// 模型供應商狀態資料傳輸物件。
/// </summary>
public sealed record ProviderStatusDto(string Id, bool Available, string? Notice);

/// <summary>
/// 系統模型目錄服務。
/// 管理已安裝的模型組態與各供應商的即時可用性（透過 HTTP 探測）。
/// 採用 Stale-While-Revalidate（SWR）快取機制：
/// - 當前快取在 <see cref="FreshFor"/>（10 秒）內時直接回傳；
/// - 過期時啟動背景單趟更新（single-flight），後續呼叫端可立即取得過期快取；
/// - 僅有首次無任何快取時呼叫端才需要同步等待更新完成。
/// </summary>
public sealed class ModelCatalog(
    InferenceRouter router,
    IOptions<InferenceOptions> options,
    ModelPresentation presentation,
    AiNexus.Platform.Diagnostics.Issues issues,
    TimeProvider? clock = null)
{
    /// <summary>快取新鮮期（10 秒）。在此期間內不發動探測。</summary>
    public static readonly TimeSpan FreshFor = TimeSpan.FromSeconds(10);

    /// <summary>供應商探測超時門檻（30 秒）。避免連線卡死導致背景 Task 永遠佔用。</summary>
    private static readonly TimeSpan DiscoveryTimeout = TimeSpan.FromSeconds(30);

    private readonly TimeProvider time = clock ?? TimeProvider.System;
    private readonly Lock gate = new();
    private Snapshot? current;
    private Task<Snapshot>? refreshing;

    private sealed record Snapshot(
        IReadOnlyList<ProviderStatusDto> Providers,
        IReadOnlySet<string> Installed,
        IReadOnlyDictionary<string, ModelDefinition> Profiles,
        DateTimeOffset CheckedAt);

    private async Task<Snapshot> SnapshotAsync(CancellationToken ct)
    {
        Task<Snapshot> pending;
        lock (gate)
        {
            var cached = current;
            if (cached is not null && time.GetUtcNow() - cached.CheckedAt <= FreshFor)
            {
                return cached;
            }

            if (refreshing is null)
            {
                // 單趟背景更新，避免多個請求同時觸發重複探測
                refreshing = pending = Task.Run(RefreshAsync, CancellationToken.None);
                // 沒有呼叫端會 await 背景刷新，失敗時必須在這裡吸收例外，否則會變成未觀察的例外。
                // 實際的錯誤記錄已由 RefreshAsync 透過 issues 回報。
                _ = pending.ContinueWith(static x => _ = x.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            }
            else
            {
                pending = refreshing;
            }

            // 若已有過期快取，立即回傳舊值以達成 Stale-While-Revalidate，呼叫端無需等待
            if (cached is not null) return cached;
        }

        // 首次啟動尚無快取時只能等待。WaitAsync 只會放棄這一次等待，不會取消共用的刷新 Task，
        // 否則其他同時等待的呼叫端會跟著失敗。
        return await pending.WaitAsync(ct);
    }

    private async Task<Snapshot> RefreshAsync()
    {
        try
        {
            using var timeout = new CancellationTokenSource(DiscoveryTimeout);

            // 並行探測 ProviderConcurrency 中登記的每個供應商
            var discovered = await Task.WhenAll(
                options.Value.ProviderConcurrency.Keys.Select(x => DiscoverAsync(x, timeout.Token)));

            // 只保留供應商實際安裝、且設定檔有列出的模型
            var installed = options.Value.Models
                .Where(m => discovered.Any(p => p.Status.Id == m.Provider && p.Models.Contains(m.NativeId)))
                .Select(m => m.Id)
                .ToHashSet(StringComparer.Ordinal);

            // 視覺能力等功能無法只靠設定檔判斷，需要向供應商實際查詢
            var profileTasks = options.Value.Models.Select(x => ResolveAsync(x, installed, timeout.Token));
            var resolvedProfiles = await Task.WhenAll(profileTasks);
            var profiles = resolvedProfiles.ToDictionary(x => x.Id, StringComparer.Ordinal);

            var snapshot = new Snapshot(
                discovered.Select(x => x.Status).ToArray(),
                installed,
                profiles,
                time.GetUtcNow());

            lock (gate)
            {
                current = snapshot;
                refreshing = null;
            }

            return snapshot;
        }
        catch (Exception error)
        {
            // 清掉 refreshing 才能讓下次呼叫重新探測；舊的 current 快取保留，繼續對外服務
            lock (gate) refreshing = null;
            issues.Report(error, "provider_discovery_unavailable", LogLevel.Warning);
            throw;
        }
    }

    private async Task<ModelDefinition> ResolveAsync(ModelDefinition profile, IReadOnlySet<string> installed, CancellationToken ct)
    {
        var images = profile.SupportsImages;
        if (profile.Provider == "ollama" && installed.Contains(profile.Id))
        {
            try
            {
                // 設定檔沒有強制覆寫時，才向 Ollama 查詢是否支援圖片；覆寫值優先
                var capabilities = await router.For(profile.Provider).CapabilitiesAsync(profile.NativeId, ct);
                images = profile.ImageCapabilityOverride != false && (capabilities?.SupportsImages ?? profile.ImageCapabilityOverride ?? false);
            }
            catch (Exception error) when (error is HttpRequestException or OperationCanceledException or JsonException or IOException or ExternalServiceException)
            {
                issues.Report(error, "provider_capability_unavailable", LogLevel.Warning);
                images = false;
            }
        }

        return Copy(profile, images);
    }

    private static ModelDefinition Copy(ModelDefinition profile, bool images) => new ModelDefinition
    {
        Id = profile.Id,
        Provider = profile.Provider,
        ProviderModelId = profile.ProviderModelId,
        DisplayName = profile.DisplayName,
        ContextTokens = profile.ContextTokens,
        MaxOutputTokens = profile.MaxOutputTokens,
        SupportsStreaming = profile.SupportsStreaming,
        SupportsUsage = profile.SupportsUsage,
        SupportsImages = images,
        ImageCapabilityOverride = profile.ImageCapabilityOverride,
        ReasoningControl = profile.ReasoningControl,
        ReasoningEfforts = profile.ReasoningEfforts,
        DefaultReasoningEffort = profile.DefaultReasoningEffort
    };

    private static ModelDefinition Current(ModelDefinition profile, Snapshot snapshot) =>
        Copy(profile, profile.Provider == "ollama"
            ? profile.ImageCapabilityOverride != false && (snapshot.Profiles.GetValueOrDefault(profile.Id)?.SupportsImages ?? false)
            : profile.SupportsImages);

    /// <summary>
    /// 取得目前系統組態中已設定的模型清單（包含動態更新後的功能特性）。
    /// </summary>
    public async Task<IReadOnlyList<ModelDefinition>> ProfilesAsync(CancellationToken ct)
    {
        var snapshot = await SnapshotAsync(ct);
        return options.Value.Models.Select(x => Current(x, snapshot)).ToArray();
    }

    /// <summary>
    /// 取得各模型供應商目前的健康與可用性狀態清單。
    /// </summary>
    public async Task<IReadOnlyList<ProviderStatusDto>> ProviderStatusesAsync(CancellationToken ct) =>
        (await SnapshotAsync(ct)).Providers;

    private async Task<(ProviderStatusDto Status, IReadOnlySet<string> Models)> DiscoverAsync(string id, CancellationToken ct)
    {
        try
        {
            var models = await router.For(id).InstalledModelsAsync(ct);
            return (new(id, true, null), models);
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException or IOException or ExternalServiceException)
        {
            var issue = issues.Report(exception, "provider_discovery_unavailable", LogLevel.Warning);
            return (new(id, false, AiNexus.Platform.Diagnostics.Issues.Message(issue)), new HashSet<string>());
        }
    }

    /// <summary>
    /// 取得模型全域目錄資訊（包含可用模型清單、供應商健康狀態與注意事項提示）。
    /// </summary>
    public async Task<ModelsDto> GetAsync(CancellationToken ct) => Describe(await SnapshotAsync(ct));

    private ModelsDto Describe(Snapshot snapshot)
    {
        var providers = snapshot.Providers;
        var allowed = options.Value.Models
            .Select(x => Current(x, snapshot))
            .Where(x => snapshot.Installed.Contains(x.Id) && (options.Value.AllowModelSelection || x.Id == presentation.DefaultId))
            .Select(presentation.Model)
            .ToList();

        var available = providers.Any(x => x.Available);
        var notice = !available
            ? (providers.Count > 0 ? providers[0].Notice : null) ?? "目前無法連線至模型服務，請稍後重試。"
            : allowed.Count == 0
                ? "系統指定的模型尚未就緒，請由管理員確認模型設定。"
                : providers.Any(x => !x.Available)
                    ? "部分模型供應商暫時無法使用，其餘模型可正常使用。"
                    : null;

        return new(
            allowed,
            available,
            notice,
            presentation.Policy,
            options.Value.ShowModelNames ? providers : []);
    }

    /// <summary>
    /// 驗證並取得請求呼叫的模型規格。
    /// 若請求未指定則選用系統預設模型；同時驗證模型是否在允許清單內且其供應商目前正常連線。
    /// </summary>
    public async Task<Result<ModelDefinition>> RequireAsync(string? id, CancellationToken ct)
    {
        var requested = id ?? presentation.Policy.DefaultModelId;

        // 若系統組態關閉「允許使用者切換模型」，且請求的模型不是預設模型時拒絕
        if (!options.Value.AllowModelSelection && requested != presentation.Policy.DefaultModelId)
        {
            return InferenceErrors.ModelSelectionDisabled;
        }

        var snapshot = await SnapshotAsync(ct);
        var catalog = Describe(snapshot);

        if (!catalog.ProviderAvailable)
        {
            return InferenceErrors.ProviderUnavailable;
        }

        var profile = options.Value.Models.FirstOrDefault(x => presentation.PublicId(x.Id) == requested);
        if (profile is not null && snapshot.Providers.Any(x => x.Id == profile.Provider && !x.Available))
        {
            return InferenceErrors.ProviderUnavailable;
        }

        if (!catalog.Models.Any(x => x.Id == requested))
        {
            return InferenceErrors.ModelNotAllowed;
        }

        return Current(options.Value.Models.Single(x => presentation.PublicId(x.Id) == requested), snapshot);
    }

    /// <summary>
    /// 檢查並決定模型推論時使用的思考/推理強度（Reasoning Effort）。
    /// </summary>
    public static Result<string> RequireReasoning(ModelDefinition model, string? effort)
    {
        var value = effort ?? model.DefaultReasoningEffort;
        return value == "auto" || model.ReasoningEfforts.Contains(value, StringComparer.Ordinal)
            ? value
            : InferenceErrors.ReasoningNotSupported;
    }
}
