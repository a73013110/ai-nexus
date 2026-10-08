using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Attachments;

public sealed record RenameLibraryFileRequest(string FileName, string ExpectedFileName);

internal sealed class RenameLibraryFileRequestValidator : RequestValidator<RenameLibraryFileRequest>
{
    public override string ProblemCode => AttachmentErrors.FileNameInvalidCode;

    public RenameLibraryFileRequestValidator()
    {
        RuleFor(x => x.FileName).Must(x => RenameLibraryFile.NameIsValid(x.Trim())).WithErrorCode("invalid");
    }
}

/// <summary>Renames a library file. The extension must stay, and the rename fails if someone renamed it first.</summary>
internal sealed class RenameLibraryFile(NexusDbContext db, AttachmentService files)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPut("/{id:guid}/name", async (Guid id, RenameLibraryFileRequest body, ICurrentUser user, RenameLibraryFile handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, id, body, ct)).ToHttpResult())
        .WithName("RenameLibraryFile").Produces<AttachmentDto>();

    /// <summary>1 to 180 characters, no path, control or reserved characters, and not ending with a dot.</summary>
    internal static bool NameIsValid(string name) => name.Length is >= 1 and <= Attachment.FileNameMaxLength && !name.Any(char.IsControl)
        && name.IndexOfAny(['/', '\\', ':', '*', '?', '"', '<', '>', '|']) < 0 && !name.EndsWith('.') && name is not ("." or "..");

    public async Task<Result<AttachmentDto>> HandleAsync(Guid actor, Guid id, RenameLibraryFileRequest request, CancellationToken ct)
    {
        var name = request.FileName.Trim();
        var found = await files.FindOwnedAsync(actor, id, ct);
        if (!found.IsSuccess) return found.Error;
        var file = found.Value;
        if (!file.InLibrary) return AttachmentErrors.LibraryFileNotFound;
        if (!string.Equals(Path.GetExtension(name), Path.GetExtension(file.FileName), StringComparison.OrdinalIgnoreCase)) return AttachmentErrors.ExtensionChanged;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var changed = await db.Set<Attachment>().Where(x => x.Id == id && x.OwnerId == actor && x.InLibrary && x.FileName == request.ExpectedFileName && x.StorageState == AttachmentStates.Ready)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.FileName, name), ct);
        if (changed != 1) return AttachmentErrors.FileNameChanged;
        db.AuditEvents.Add(new() { OwnerId = actor, ResourceId = id, Action = "file.renamed", Result = "saved" });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        file.FileName = name;
        return AttachmentService.Describe(file);
    }
}
