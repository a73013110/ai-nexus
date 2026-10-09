using AiNexus.Features.Jobs;

namespace AiNexus.Features.Knowledge;

public sealed class DocumentEmbeddingHandler(DocumentService documents, DocumentIndexer indexer) : IBackgroundJobHandler
{
    public string Kind => "document-embedding";
    public async Task ValidateRetryAsync(BackgroundJob job, CancellationToken ct) => _ = await documents.RequireAsync(job.OwnerId, job.SubjectId, ct, write: true);
    public async Task ExecuteAsync(JobExecution execution, CancellationToken ct)
    {
        var document = await documents.RequireAsync(execution.Job.OwnerId, execution.Job.SubjectId, ct, write: true);
        await indexer.IndexCurrentAsync(execution, document, ct);
    }
}
