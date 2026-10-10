using AiNexus.Features.Inference;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Knowledge.Embeddings;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA1001:Types that own disposable fields should be disposable", Justification = "SemaphoreSlim without AvailableWaitHandle holds nothing to release; disposing a shared gate would throw in work still releasing it during shutdown.")]
public sealed class EmbeddingBatchScheduler(IOptions<KnowledgeOptions> options, GenerationScheduler generation)
{
    private readonly SemaphoreSlim gate = new(options.Value.Embedding.MaxConcurrentBatches);
    public async Task<IDisposable> EnterAsync(CancellationToken ct)
    {
        while (true)
        {
            while (generation.Generating || generation.QueueDepth > 0) await Task.Delay(200, ct);
            await gate.WaitAsync(ct);
            if (!generation.Generating && generation.QueueDepth == 0) return new Lease(gate);
            gate.Release();
        }
    }
    private sealed class Lease(SemaphoreSlim gate) : IDisposable { public void Dispose() => gate.Release(); }
}
