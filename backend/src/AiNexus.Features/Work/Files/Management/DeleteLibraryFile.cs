using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Work.Files.Management;

/// <summary>從檔案庫刪除檔案；與移除草稿同一套規則（<see cref="RemoveAttachment"/>），只是授權在檔案庫頁。</summary>
internal sealed class DeleteLibraryFile : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapDelete("/{id:guid}", (Guid id, ICurrentUser user, RemoveAttachment handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, id, fromLibrary: true, ct))
        .Audited(FilesAudit.FileDeleted)
        .WithName("DeleteLibraryFile");
}
