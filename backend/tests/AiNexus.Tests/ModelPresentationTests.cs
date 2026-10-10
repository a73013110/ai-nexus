using AiNexus.Features.Chat;
using AiNexus.Features.Conversations;
using AiNexus.Features.Inference;
using AiNexus.Features.Knowledge;
using Microsoft.Extensions.Options;
using Xunit;

namespace AiNexus.Tests;

public sealed class ModelPresentationTests
{
    [Theory]
    [InlineData(true, "本地助理")]
    [InlineData(false, "AI 助理 1")]
    public void ReferencesCarryDisplayNamesWithoutChangingRouting(bool visible, string label)
    {
        const string id = "ollama/hf.co/provider/full-technical-model:Q3_K_XL";
        var presentation = new ModelPresentation(Options.Create(new InferenceOptions
        {
            ShowModelNames = visible,
            Models = [new() { Id = id, Provider = "ollama", ProviderModelId = "native-private-model", DisplayName = "本地助理" }]
        }), [KnowledgeModule.EmbeddingModel(new KnowledgeOptions { Embedding = new() { Model = "private-embedding-id" } })]);
        var reference = presentation.PublicId(id);
        Assert.Equal(id, presentation.InternalId(reference));
        Assert.Equal(label, presentation.Model(new() { Id = id }).DisplayName);
        Assert.Equal(label, presentation.Run(new() { ModelId = id }).ModelDisplayName);
        Assert.Equal(label, presentation.Message(new Message { ModelId = id }).ModelDisplayName);
        Assert.Null(presentation.Message(new Message()).ModelDisplayName);
        Assert.Equal("本地助理", presentation.DisplayName(id, administrator: true));
        Assert.Equal(label, presentation.DisplayName("native-private-model", provider: "ollama"));
        Assert.Equal("已停用的模型", presentation.DisplayName("native-private-model", provider: "google"));
        Assert.Equal("已停用的模型", presentation.DisplayName("removed/private-model"));
        Assert.Equal("知識向量模型", presentation.DisplayName("private-embedding-id"));
        Assert.NotEqual(presentation.PublicId("removed/private-model"), presentation.PublicId("private-embedding-id"));
        Assert.Equal("網路搜尋", presentation.DisplayName("web-search"));
    }
}
