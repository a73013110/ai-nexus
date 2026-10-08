using AiNexus.Features.Identity;

namespace AiNexus.Features.Attachments;

public static class FileLibraryEndpoints
{
    public static void MapFileLibrary(RouteGroupBuilder api)
    {
        var routes = api.MapGroup("/files").RequireAuthorization("feature:files").WithTags("File library");
        routes.MapPut("/{id:guid}/name", async (Guid id, RenameLibraryFileRequest body, CurrentUser current, FileLibraryService library, CancellationToken ct) =>
            await library.RenameAsync((await current.GetAsync(ct)).Id, id, body, ct)).WithName("RenameLibraryFile").Produces<AttachmentDto>();
        routes.MapGet("", async (string? search, string? type, string? source, int? offset, int? limit, CurrentUser current, FileLibraryService library, CancellationToken ct) =>
            await library.ListAsync((await current.GetAsync(ct)).Id, search, type ?? "all", source ?? "all", offset ?? 0, limit ?? 40, ct))
            .WithName("ListLibraryFiles").Produces<FileLibraryPageDto>();
        routes.MapPost("/{id:guid}/retain", async (Guid id, CurrentUser current, FileLibraryService library, CancellationToken ct) =>
        { await library.RetainAsync((await current.GetAsync(ct)).Id, id, ct); return Results.NoContent(); }).WithName("RetainLibraryFile").Produces(204);
        routes.MapDelete("/{id:guid}", async (Guid id, CurrentUser current, AttachmentService files, CancellationToken ct) =>
        { await files.RemoveDraftAsync((await current.GetAsync(ct)).Id, id, ct, fromLibrary: true); return Results.NoContent(); }).WithName("DeleteLibraryFile").Produces(204);
    }
}
