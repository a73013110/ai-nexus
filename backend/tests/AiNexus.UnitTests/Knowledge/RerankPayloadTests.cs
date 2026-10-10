using System.Text.Json;
using AiNexus.Features.Knowledge.Retrieval;
using AiNexus.Platform.Errors;

namespace AiNexus.UnitTests.Knowledge;

public sealed class RerankPayloadTests
{
    [Theory]
    [InlineData("{}")]
    [InlineData("[{}]")]
    [InlineData("[{\"index\":0,\"score\":\"bad\"}]")]
    [InlineData("[{\"index\":0,\"score\":1e999}]")]
    public void MalformedRerankerPayloadUsesDeclaredFailurePolicy(string json)
    {
        using var payload = JsonDocument.Parse(json);
        Assert.Equal("rerank_invalid", Assert.Throws<ExternalServiceException>(() => RerankPayload.Parse(payload.RootElement, "score")).Error.Code);
    }
}
