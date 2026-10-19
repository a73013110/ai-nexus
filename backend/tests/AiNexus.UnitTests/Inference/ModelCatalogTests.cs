using AiNexus.Features.Platform.Inference;
using AiNexus.Features.Platform.Inference.Models;
using AiNexus.Features.Platform.Inference.Providers;
using AiNexus.Features.Work.Knowledge;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AiNexus.UnitTests.Inference;

public sealed class ModelCatalogTests
{
    [Theory]
    [InlineData(true, null, true)]
    [InlineData(false, null, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public async Task NativeCapabilitiesDriveBothCatalogAndGeneration(bool vision, bool? configured, bool expected)
    {
        var options = OllamaOptions(configured);
        var provider = new CapabilityProvider(vision);
        var (catalog, container) = Create(options, provider);
        using var _ = container;
        var dto = Assert.Single((await catalog.GetAsync(CancellationToken.None)).Models);
        Assert.Equal(expected, dto.SupportsImages); Assert.Equal("model-1", dto.Id); Assert.Equal("AI 助理 1", dto.DisplayName);
        Assert.Equal(expected, (await catalog.RequireAsync("model-1", CancellationToken.None)).Value!.SupportsImages);
        Assert.Equal(1, provider.Checks);
        // 模型能力會快取，但生成參數（如 MaxOutputTokens）每次都讀取目前的選項，改設定後不必等快取過期。
        options.Value.Models[0].MaxOutputTokens = 128;
        Assert.Equal(128, (await catalog.RequireAsync("model-1", CancellationToken.None)).Value!.MaxOutputTokens);
    }

    [Fact]
    public async Task StaleCatalogIsServedAtOnceWhileOneBackgroundRefreshRuns()
    {
        var provider = new SlowDiscovery();
        var clock = new ManualClock();
        var (catalog, container) = Create(OllamaOptions(), provider, clock: clock);
        using var _ = container;
        Assert.Single((await catalog.GetAsync(CancellationToken.None)).Models);
        Assert.Equal(1, provider.Calls);
        clock.Now += ModelCatalog.FreshFor + TimeSpan.FromSeconds(1);
        provider.Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        // 模型探索卡住時，呼叫端仍要立即拿到快取的模型；而且重複呼叫不會讓背景刷新多啟動一次。
        for (var i = 0; i < 3; i++) Assert.Single((await catalog.GetAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5))).Models);
        await provider.SecondCall.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(2, provider.Calls);
        provider.Pending.SetResult(new HashSet<string>());
        for (var i = 0; i < 200 && (await catalog.GetAsync(CancellationToken.None)).Models.Count > 0; i++) await Task.Delay(10);
        Assert.Empty((await catalog.GetAsync(CancellationToken.None)).Models);
        Assert.Equal(2, provider.Calls);
    }

    [Fact]
    public async Task RefusalNamesTheProviderAndItsDiscoveryIssue()
    {
        var logger = new CaptureLogger();
        var (catalog, container) = Create(OllamaOptions(), new UnreachableProvider(), logger);
        using var _ = container;

        var refused = await catalog.RequireAsync(null, CancellationToken.None);

        // 背景工作最後只記 provider_unavailable；同 Trace 的這筆事件要能指出供應商與探測失敗的查證代碼。
        Assert.Equal("provider_unavailable", refused.Error!.Code);
        var (id, state) = Assert.Single(logger.Events);
        Assert.Equal(3200, id);
        Assert.Equal("ollama", state["Provider"]);
        Assert.True(AiNexus.Platform.Diagnostics.Issues.ValidCode(state["Reason"] as string));
    }

    // 單一 Ollama 模型、隱藏模型名稱的設定。
    private static IOptions<InferenceOptions> OllamaOptions(bool? images = null) => Options.Create(new InferenceOptions
    {
        ProviderConcurrency = new() { ["ollama"] = 1 },
        ShowModelNames = false,
        Models = [new() { Id = "ollama/test-model", Provider = "ollama", ProviderModelId = "test-model", ImageCapabilityOverride = images }]
    });

    // 回傳的容器由呼叫端處置。
    private static (ModelCatalog Catalog, ServiceProvider Container) Create(IOptions<InferenceOptions> options, IInferenceProvider provider, ILogger<ModelCatalog>? logger = null, TimeProvider? clock = null)
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton("ollama", provider);
        var container = services.BuildServiceProvider();
        var issues = new AiNexus.Platform.Diagnostics.Issues(NullLogger<AiNexus.Platform.Diagnostics.Issues>.Instance);
        var presentation = new ModelPresentation(options, [KnowledgeModule.EmbeddingModel(new KnowledgeOptions())]);
        return (new ModelCatalog(new InferenceRouter(container, options), options, presentation, issues, logger ?? NullLogger<ModelCatalog>.Instance, clock), container);
    }

    private sealed class CaptureLogger : ILogger<ModelCatalog>
    {
        public List<(int Id, Dictionary<string, object?> State)> Events { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Events.Add((eventId.Id, ((IEnumerable<KeyValuePair<string, object?>>)state!).ToDictionary(x => x.Key, x => x.Value)));
    }

    private sealed class UnreachableProvider : IInferenceProvider
    {
        public Task<IReadOnlySet<string>> InstalledModelsAsync(CancellationToken ct) => throw new HttpRequestException("unreachable");
        public IAsyncEnumerable<InferenceChunk> StreamAsync(string model, IReadOnlyList<InferenceMessage> messages, GenerationParameters parameters, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now = new(2026, 10, 9, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class SlowDiscovery : IInferenceProvider
    {
        public int Calls;
        public TaskCompletionSource<IReadOnlySet<string>>? Pending;
        public TaskCompletionSource SecondCall { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<IReadOnlySet<string>> InstalledModelsAsync(CancellationToken ct)
        {
            if (Interlocked.Increment(ref Calls) == 2) SecondCall.TrySetResult();
            return Pending?.Task ?? Task.FromResult<IReadOnlySet<string>>(new HashSet<string> { "test-model" });
        }
        public IAsyncEnumerable<InferenceChunk> StreamAsync(string model, IReadOnlyList<InferenceMessage> messages, GenerationParameters parameters, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class CapabilityProvider(bool vision) : IInferenceProvider
    {
        public int Checks;
        public Task<IReadOnlySet<string>> InstalledModelsAsync(CancellationToken ct) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string> { "test-model" });
        public Task<ModelCapabilities?> CapabilitiesAsync(string model, CancellationToken ct) { Assert.Equal("test-model", model); Checks++; return Task.FromResult<ModelCapabilities?>(new(vision)); }
        public IAsyncEnumerable<InferenceChunk> StreamAsync(string model, IReadOnlyList<InferenceMessage> messages, GenerationParameters parameters, CancellationToken ct) => throw new NotSupportedException();
    }
}
