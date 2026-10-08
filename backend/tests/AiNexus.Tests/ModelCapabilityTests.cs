using AiNexus.Features.Inference;
using AiNexus.Features.Knowledge;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AiNexus.Tests;

public sealed class ModelCapabilityTests
{
    [Theory]
    [InlineData(true, null, true)]
    [InlineData(false, null, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public async Task NativeCapabilitiesDriveBothCatalogAndGeneration(bool vision, bool? configured, bool expected)
    {
        var options = Options.Create(new InferenceOptions { ProviderConcurrency = new() { ["ollama"] = 1 }, ShowModelNames = false,
            Models = [new() { Id = "ollama/test-model", Provider = "ollama", ProviderModelId = "test-model", ImageCapabilityOverride = configured }] });
        var provider = new CapabilityProvider(vision);
        var services = new ServiceCollection(); services.AddKeyedSingleton<IInferenceProvider>("ollama", provider);
        using var container = services.BuildServiceProvider();
        var catalog = new ModelCatalog(new InferenceRouter(container, options), options, new ModelPresentation(options, Options.Create(new KnowledgeOptions())), new AiNexus.Platform.Diagnostics.Issues(Microsoft.Extensions.Logging.Abstractions.NullLogger<AiNexus.Platform.Diagnostics.Issues>.Instance));
        var dto = Assert.Single((await catalog.GetAsync(CancellationToken.None)).Models);
        Assert.Equal(expected, dto.SupportsImages); Assert.Equal("model-1", dto.Id); Assert.Equal("AI 助理 1", dto.DisplayName);
        Assert.Equal(expected, (await catalog.RequireAsync("model-1", CancellationToken.None)).SupportsImages);
        Assert.Equal(1, provider.Checks);
        // Capabilities are cached; configured generation parameters still reflect current options.
        options.Value.Models[0].MaxOutputTokens = 128;
        Assert.Equal(128, (await catalog.RequireAsync("model-1", CancellationToken.None)).MaxOutputTokens);
    }
    private sealed class CapabilityProvider(bool vision) : IInferenceProvider
    {
        public int Checks;
        public Task<IReadOnlySet<string>> InstalledModelsAsync(CancellationToken ct) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string> { "test-model" });
        public Task<ModelCapabilities?> CapabilitiesAsync(string model, CancellationToken ct) { Assert.Equal("test-model", model); Checks++; return Task.FromResult<ModelCapabilities?>(new(vision)); }
        public IAsyncEnumerable<InferenceChunk> StreamAsync(string model, IReadOnlyList<InferenceMessage> messages, GenerationParameters parameters, CancellationToken ct) => throw new NotSupportedException();
    }
}
