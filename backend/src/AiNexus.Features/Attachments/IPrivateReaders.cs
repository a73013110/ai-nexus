namespace AiNexus.Features.Attachments;

/// <summary>A standalone document that only displays its owner's attachment.</summary>
public sealed class PrivateReader
{
    public Guid Id { get; init; }
    public Guid OwnerId { get; init; }
}

/// <summary>
/// Private readers, deleted or not. Knowledge implements it, so attachment cleanup can treat a file read only by its
/// owner's readers as a draft without depending on the knowledge module.
/// </summary>
public interface IPrivateReaders
{
    IQueryable<PrivateReader> All { get; }

    /// <summary>Deletes the owner's readers of <paramref name="files"/> with their content, references and active jobs, inside the caller's transaction.</summary>
    Task RemoveAsync(Guid owner, IReadOnlyList<Guid> files, CancellationToken ct);
}
