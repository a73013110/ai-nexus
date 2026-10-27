using AiNexus.Data.App;
using AiNexus.Data.App.Files;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Files.Management;

public sealed record RenameLibraryFileRequest(string FileName, string ExpectedFileName);

internal sealed class RenameLibraryFileRequestValidator : RequestValidator<RenameLibraryFileRequest>
{
    public override string ProblemCode => FilesErrors.FileNameInvalidCode;

    public RenameLibraryFileRequestValidator()
    {
        RuleFor(x => x.FileName).Must(x => RenameLibraryFile.NameIsValid(x.Trim())).WithErrorCode("invalid");
    }
}

/// <summary>重新命名檔案庫中的檔案。副檔名必須保留；若別人已先改名，這次改名會失敗。</summary>
internal sealed class RenameLibraryFile(AppDbContext db, AttachmentService files)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPut("/{id:guid}/name", async (Guid id, RenameLibraryFileRequest body, ICurrentUser user, RenameLibraryFile handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, id, body, ct)).ToHttpResult())
        .Audited(FilesAudit.FileRenamed)
        .WithName("RenameLibraryFile");

    /// <summary>1 到 180 個字元，不可含路徑、控制字元或保留字元，且不可以點結尾。</summary>
    internal static bool NameIsValid(string name) => name.Length is >= 1 and <= Attachment.FileNameMaxLength && !name.Any(char.IsControl)
        && name.IndexOfAny(['/', '\\', ':', '*', '?', '"', '<', '>', '|']) < 0 && !name.EndsWith('.') && name is not ("." or "..");

    public async Task<Result<AttachmentDto>> HandleAsync(Guid actor, Guid id, RenameLibraryFileRequest request, CancellationToken ct)
    {
        var name = request.FileName.Trim();
        var found = await files.OwnedAsync(actor, id, ct);
        if (!found.IsSuccess) return found.Error;
        var file = found.Value;
        if (!file.InLibrary) return FilesErrors.LibraryFileNotFound;
        if (!string.Equals(Path.GetExtension(name), Path.GetExtension(file.FileName), StringComparison.OrdinalIgnoreCase)) return FilesErrors.ExtensionChanged;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var changed = await db.Set<Attachment>().Where(x => x.Id == id && x.OwnerId == actor && x.InLibrary && x.FileName == request.ExpectedFileName && x.StorageState == AttachmentStates.Ready)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.FileName, name), ct);
        if (changed != 1) return FilesErrors.FileNameChanged;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        file.FileName = name;
        return AttachmentService.Describe(file);
    }
}
