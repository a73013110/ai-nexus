using AiNexus.Features.Jobs;
using AiNexus.Features.Knowledge.Documents;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Knowledge.Indexing;

public sealed class DocumentEmbeddingHandler(DocumentService documents, DocumentIndexer indexer) : IBackgroundJobHandler
{
    public string Kind => "document-embedding";
    public async Task<Result> ValidateRetryAsync(BackgroundJob job, CancellationToken ct) { _ = await documents.RequireAsync(job.OwnerId, job.SubjectId, ct, write: true); return Result.Success; }
    public async Task<Result> ExecuteAsync(JobExecution execution, CancellationToken ct)
    {
        var document = await documents.RequireAsync(execution.Job.OwnerId, execution.Job.SubjectId, ct, write: true);
        await indexer.IndexCurrentAsync(execution, document, ct);
        return Result.Success;
    }
}
