using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Http;

namespace AiNexus.Features.Knowledge.Documents;

public sealed record TextDocumentDto(Guid Id, string Title, string Text, int Version);

/// <summary>The editable text and version of a plain-text source the user may read.</summary>
internal sealed class ReadTextDocument(DocumentAccess documents)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/{id:guid}/text", (Guid id, ICurrentUser user, ReadTextDocument handler, CancellationToken ct) => handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync())
        .WithRequestBodyLimit(RequestBodyLimits.ForJsonCharacters(TextDocuments.MaxCharacters)).WithName("ReadTextDocument");

    public async Task<Result<TextDocumentDto>> HandleAsync(Guid actor, Guid id, CancellationToken ct)
    {
        var document = await documents.FindAsync(actor, id, ct);
        if (!document.IsSuccess) return document.Error;
        if (document.Value.TextContent is null) return KnowledgeErrors.NotEditableText;
        return new TextDocumentDto(id, Path.GetFileNameWithoutExtension(document.Value.FileName), document.Value.TextContent, document.Value.TextVersion);
    }
}
