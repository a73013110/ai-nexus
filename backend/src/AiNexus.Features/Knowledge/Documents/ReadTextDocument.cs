using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Http;

namespace AiNexus.Features.Knowledge.Documents;

/// <summary>The editable text and version of a plain-text source the user may read.</summary>
internal static class ReadTextDocument
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}/text", async (Guid id, ICurrentUser user, DocumentAccess documents, CancellationToken ct) =>
            (await HandleAsync(documents, user.Id, id, ct)).ToHttpResult())
        .WithRequestBodyLimit(RequestBodyLimits.ForJsonCharacters(TextDocuments.MaxCharacters)).WithName("ReadTextDocument").Produces<TextDocumentDto>();

    private static async Task<Result<TextDocumentDto>> HandleAsync(DocumentAccess documents, Guid actor, Guid id, CancellationToken ct)
    {
        var document = await documents.FindAsync(actor, id, ct);
        if (!document.IsSuccess) return document.Error;
        if (document.Value.TextContent is null) return KnowledgeErrors.NotEditableText;
        return new TextDocumentDto(id, Path.GetFileNameWithoutExtension(document.Value.FileName), document.Value.TextContent, document.Value.TextVersion);
    }
}

public sealed record TextDocumentDto(Guid Id, string Title, string Text, int Version);
