using AiNexus.Platform.Http;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Security;
using AiNexus.Features.Identity;

namespace AiNexus.Features.Attachments;

public static class AttachmentEndpoints
{
    public static void MapAttachments(this RouteGroupBuilder api)
    {
        FileLibraryEndpoints.MapFileLibrary(api);
        var routes = api.MapGroup("/attachments").RequireAuthorization("feature:attachments").WithTags("Attachments");
        routes.MapGet("/policy", (AttachmentService files) => Results.Ok(files.Policy)).WithName("AttachmentPolicy").Produces<AttachmentPolicyDto>();
        routes.MapGet("/storage", async (CurrentUser current, AttachmentQuota quota, CancellationToken ct) => Results.Ok(await quota.ForAsync((await current.GetAsync(ct)).Id, ct))).WithName("GetAttachmentStorage").Produces<AttachmentStorageDto>();
        // The application's header-based antiforgery middleware validates this upload as well.
        routes.MapPost("", async (HttpContext http, CurrentUser current, AttachmentService files, CancellationToken ct) =>
        {
            if (!http.Request.HasFormContentType) throw new ApiException(400, "multipart_required", "請使用檔案上傳格式。");
            var form = await http.Request.ReadFormAsync(ct);
            if (form.Files.Count != 1) throw new ApiException(400, "file_required", "每次上傳請提供一個檔案。");
            return Results.Ok(await files.UploadAsync((await current.GetAsync(ct)).Id, form.Files[0], ct));
        }).WithRequestBodyLimit(AttachmentsModule.UploadBodyLimit).WithName("UploadAttachment").Produces<AttachmentDto>();
        routes.MapGet("/{id:guid}", async (Guid id, CurrentUser current, AttachmentService files, CancellationToken ct) => Results.Ok(AttachmentService.Describe(await files.OwnedAsync((await current.GetAsync(ct)).Id, id, ct)))).WithName("GetAttachment").Produces<AttachmentDto>();
        routes.MapGet("/{id:guid}/content", async (Guid id, bool? download, HttpContext http, CurrentUser current, AttachmentService files, CancellationToken ct) =>
        {
            var file = await files.OwnedAsync((await current.GetAsync(ct)).Id, id, ct);
            return WebSecurity.File(http, await files.OpenAsync(file, ct), file.ContentType, file.FileName, download == true);
        }).WithName("DownloadAttachment");
        routes.MapDelete("/{id:guid}", async (Guid id, CurrentUser current, AttachmentService files, CancellationToken ct) =>
        {
            await files.RemoveDraftAsync((await current.GetAsync(ct)).Id, id, ct);
            return Results.NoContent();
        }).WithName("RemoveDraftAttachment").Produces(204);
    }
}
