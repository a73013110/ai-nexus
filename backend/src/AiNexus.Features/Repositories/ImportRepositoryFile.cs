using AiNexus.Platform.Validation;
using System.Text;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Attachments;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Knowledge.Documents;

namespace AiNexus.Features.Repositories;

[ValidatedInHandler("RepositoryService.FileAsync checks repository, commit and path with the same public codes as GET /repositories/file.")]
public sealed record RepositoryImportRequest(string Repository, string Commit, string Path, Guid CollectionId);

/// <summary>Imports one file at a pinned commit into a knowledge collection as an independent snapshot. Re-importing returns the existing document.</summary>
internal sealed class ImportRepositoryFile(NexusDbContext db, RepositoryService gitea, AttachmentService attachments, DocumentService documents,
    ResourceAccess access, RepositoryWriteLock writes)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("/import", async (RepositoryImportRequest body, ICurrentUser user, ImportRepositoryFile handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, body, ct)).ToHttpResult())
        .RequireAuthorization(Policies.Knowledge).WithName("ImportRepositoryFile");

    public async Task<Result<DocumentDto>> HandleAsync(Guid owner, RepositoryImportRequest body, CancellationToken ct)
    {
        if (await access.RequireAsync(owner, body.CollectionId, "knowledge", ct, write: true) is { IsSuccess: false } denied) return denied.Error;
        // Read at the pinned commit again: browser content is never trusted as a source snapshot.
        var read = await gitea.FileAsync(owner, body.Repository, body.Commit, body.Path, ct);
        if (!read.IsSuccess) return read.Error;
        var file = read.Value;
        await writes.Gate.WaitAsync(ct);
        try
        {
            var old = await db.Set<RepositoryImport>().AsNoTracking().Where(x => x.OwnerId == owner && x.CollectionId == body.CollectionId && x.Repository == file.Repository && x.Commit == file.Commit && x.Path == file.Path && x.BaseUrl == gitea.BaseUrl)
                .Join(db.Set<KnowledgeDocument>().Where(x => !x.IsDeleted), x => x.DocumentId, d => d.Id, (x, d) => x.DocumentId).FirstOrDefaultAsync(ct);
            if (old != Guid.Empty) return await documents.DetailAsync(owner, old, ct);
            var text = $"Gitea: {file.Repository}\nPath: {file.Path}\nCommit: {file.Commit}\nSource: {file.Url}\n\n{file.Text}";
            var bytes = Encoding.UTF8.GetBytes(text); using var stream = new MemoryStream(bytes);
            var upload = new FormFile(stream, 0, bytes.Length, "file", string.Concat((file.Repository.Replace('/', '_') + "_" + Path.GetFileName(file.Path)).Take(170)) + ".txt") { Headers = new HeaderDictionary(), ContentType = "text/plain" };
            var attachment = await attachments.UploadAsync(owner, upload, ct);
            if (!attachment.IsSuccess) return attachment.Error;
            var added = await documents.AddAsync(owner, body.CollectionId, attachment.Value.Id, ct);
            if (!added.IsSuccess) return added.Error;
            var document = added.Value;
            db.Add(new RepositoryImport { OwnerId = owner, CollectionId = body.CollectionId, DocumentId = document.Id, Repository = file.Repository, Commit = file.Commit, Path = file.Path, BaseUrl = gitea.BaseUrl });
            db.AuditEvents.Add(new AuditEvent { OwnerId = owner, ResourceId = document.Id, Action = "repository.imported", Result = "snapshot" });
            await db.SaveChangesAsync(ct);
            return document;
        }
        finally { writes.Gate.Release(); }
    }
}
