using AiNexus.Features.Attachments;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Http;
using AiNexus.Features.Knowledge.Collections;

namespace AiNexus.Features.Knowledge.Documents;

/// <summary>Creates a plain-text source in a collection the user may edit: the text is uploaded as a .txt attachment and queued for indexing.</summary>
internal sealed class CreateTextDocument(ResourceAccess access, AttachmentService attachments, AddKnowledgeDocument add)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("/collections/{id:guid}/text", async (Guid id, TextDocumentRequest body, ICurrentUser user, CreateTextDocument handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, id, body, ct)).ToHttpResult())
        .WithRequestBodyLimit(RequestBodyLimits.ForJsonCharacters(TextDocuments.MaxCharacters)).WithName("CreateTextDocument").Produces<DocumentDto>();

    public async Task<Result<DocumentDto>> HandleAsync(Guid actor, Guid collection, TextDocumentRequest request, CancellationToken ct)
    {
        await access.RequireAsync(actor, collection, KnowledgeCollection.Kind, ct, write: true);
        var source = TextDocuments.Clean(request);
        if (!source.IsSuccess) return source.Error;
        var file = await TextDocuments.UploadAsync(attachments, actor, source.Value, ct);
        return await add.HandleAsync(actor, collection, file.Id, ct, text: source.Value);
    }
}
