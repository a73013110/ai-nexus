using AiNexus.Features.Attachments;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Knowledge.Documents;

/// <summary>
/// The module's document contract for other modules (projects, repository imports) and the ingest and embedding jobs;
/// the module's own endpoints are the slices next to this file.
/// </summary>
public sealed class DocumentService
{
    private readonly DocumentAccess documents;
    private readonly AddKnowledgeDocument add;

    internal DocumentService(DocumentAccess documents, AddKnowledgeDocument add) => (this.documents, this.add) = (documents, add);

    /// <summary>Adds an attachment as a document of a collection, of a project, or as a standalone document (both null); see <see cref="AddKnowledgeDocument"/>.</summary>
    public Task<Result<DocumentDto>> AddAsync(Guid actor, Guid? collection, Guid attachment, CancellationToken ct, Guid? project = null, TextDocumentRequest? text = null)
        => add.HandleAsync(actor, collection, attachment, ct, project, text);

    /// <summary>The tracked document, checked like every document endpoint.</summary>
    public Task<Result<KnowledgeDocument>> RequireAsync(Guid actor, Guid id, CancellationToken ct, bool write = false) => documents.FindAsync(actor, id, ct, write);

    public Task<Result<DocumentDto>> DetailAsync(Guid actor, Guid id, CancellationToken ct) => documents.DetailAsync(actor, id, ct);

    /// <summary><see cref="DetailAsync"/> for a list, in its order, in a fixed number of queries.</summary>
    public Task<Result<IReadOnlyList<DocumentDto>>> DetailsAsync(Guid actor, IReadOnlyList<Guid> ids, CancellationToken ct) => documents.DetailsAsync(actor, ids, ct);

    public Task<Result<Attachment>> OriginalAsync(Guid actor, Guid id, CancellationToken ct) => documents.OriginalAsync(actor, id, ct);
}
