using AiNexus.Features.Attachments;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Http;
using AiNexus.Features.Knowledge.Collections;

namespace AiNexus.Features.Knowledge.Documents;

/// <summary>在使用者可編輯的知識庫中建立純文字來源：文字以 .txt 附件上傳，並排入索引工作。</summary>
internal sealed class CreateTextDocument(ResourceAccess access, AttachmentService attachments, AddKnowledgeDocument add)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("/collections/{id:guid}/text", async (Guid id, TextDocumentRequest body, ICurrentUser user, CreateTextDocument handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, id, body, ct)).ToHttpResult())
        .WithRequestBodyLimit(RequestBodyLimits.ForJsonCharacters(TextDocuments.MaxCharacters)).WithName("CreateTextDocument");

    public async Task<Result<DocumentDto>> HandleAsync(Guid actor, Guid collection, TextDocumentRequest request, CancellationToken ct)
    {
        var allowed = await access.RequireAsync(actor, collection, KnowledgeCollection.Kind, ct, write: true);
        if (!allowed.IsSuccess) return allowed.Error;
        var source = TextDocuments.Clean(request);
        if (!source.IsSuccess) return source.Error;
        var file = await TextDocuments.UploadAsync(attachments, actor, source.Value, ct);
        if (!file.IsSuccess) return file.Error;
        return await add.HandleAsync(actor, collection, file.Value.Id, ct, text: source.Value);
    }
}
