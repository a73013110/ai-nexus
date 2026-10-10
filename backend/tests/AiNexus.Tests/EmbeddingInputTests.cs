using AiNexus.Features.Knowledge;
using Xunit;
using AiNexus.Features.Knowledge.Embeddings;

namespace AiNexus.Tests;

public sealed class EmbeddingInputTests
{
    [Fact]
    public void QwenInstructionAppliesOnlyToQueriesAndChangesIndexProfile()
    {
        var options = new KnowledgeOptions { Embedding = new() { Provider = "ollama", Model = "qwen3-embedding:0.6b", Dimensions = 768 } };
        var legacy = EmbeddingInput.Profile(options); Assert.StartsWith("ollama:qwen3-embedding:0.6b:768:", legacy);
        options.Embedding.InputFormat = "qwen-query"; Assert.NotEqual(legacy, EmbeddingInput.Profile(options)); Assert.StartsWith("Instruct:", EmbeddingInput.Format(options, "公文問題", false)); Assert.Equal("公文內容", EmbeddingInput.Format(options, "公文內容", true));
        var configured = EmbeddingInput.Profile(options); options.Embedding.QueryInstruction = "Retrieve relevant Traditional Chinese documents"; Assert.NotEqual(configured, EmbeddingInput.Profile(options));
        options.Embedding.Revision = "new-model-digest"; Assert.NotEqual(configured, EmbeddingInput.Profile(options));
    }
    [Fact]
    public void BgeUsesFullDimensionsAndPlainInput()
    {
        var options = new KnowledgeOptions { Embedding = new() { Provider = "ollama", Model = "bge-m3", Dimensions = 1024 } };
        Assert.StartsWith("ollama:bge-m3:1024:", EmbeddingInput.Profile(options)); Assert.Equal("校務規定", EmbeddingInput.Format(options, "校務規定", false));
    }
}
