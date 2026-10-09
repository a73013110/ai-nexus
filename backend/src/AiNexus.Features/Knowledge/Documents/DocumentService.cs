using AiNexus.Features.Attachments;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Knowledge.Documents;

/// <summary>
/// The module's document contract for other modules (projects, repository imports) and the ingest and embedding jobs.
/// Failures are thrown as <see cref="ApiException"/> for callers that cannot return a result; the module's own endpoints
/// are the slices next to this file.
/// </summary>
public sealed class DocumentService
{
    private readonly DocumentAccess documents;
    private readonly AddKnowledgeDocument add;

    internal DocumentService(DocumentAccess documents, AddKnowledgeDocument add) => (this.documents, this.add) = (documents, add);

    /// <summary>Adds an attachment as a document of a collection, of a project, or as a standalone document (both null); see <see cref="AddKnowledgeDocument"/>.</summary>
    public async Task<DocumentDto> AddAsync(Guid actor, Guid? collection, Guid attachment, CancellationToken ct, Guid? project = null, TextDocumentRequest? text = null)
        => Value(await add.HandleAsync(actor, collection, attachment, ct, project, text));

    /// <summary>The tracked document, checked like every document endpoint.</summary>
    public async Task<KnowledgeDocument> RequireAsync(Guid actor, Guid id, CancellationToken ct, bool write = false)
        => Value(await documents.FindAsync(actor, id, ct, write));

    public async Task<DocumentDto> DetailAsync(Guid actor, Guid id, CancellationToken ct) => Value(await documents.DetailAsync(actor, id, ct));

    /// <summary><see cref="DetailAsync"/> for a list, in its order, in a fixed number of queries.</summary>
    public async Task<IReadOnlyList<DocumentDto>> DetailsAsync(Guid actor, IReadOnlyList<Guid> ids, CancellationToken ct) => Value(await documents.DetailsAsync(actor, ids, ct));

    public async Task<Attachment> OriginalAsync(Guid actor, Guid id, CancellationToken ct) => Value(await documents.OriginalAsync(actor, id, ct));

    private static T Value<T>(Result<T> result) => result.IsSuccess ? result.Value : throw result.Error.ToException();
}
