using AiNexus.Platform.Validation;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Knowledge.Collections;

[ValidatedInHandler("An edit checks the body only after collection access (a 404/403 must win over a 400), and create shares the body; the order is name, then description.")]
public sealed record CollectionRequest(string Name, string Description);

/// <summary>Creates a personal collection (up to the configured limit), or renames and describes one the user may edit.</summary>
internal sealed class SaveKnowledgeCollection(NexusDbContext db, ResourceAccess access, IOptions<KnowledgeOptions> options, TimeProvider clock)
{
    public static RouteHandlerBuilder MapCreate(RouteGroupBuilder routes) => routes
        .MapPost("/collections", async (CollectionRequest request, ICurrentUser user, SaveKnowledgeCollection handler, CancellationToken ct) =>
            (await handler.CreateAsync(user.Id, request, ct)).ToHttpResult())
        .WithName("CreateKnowledgeCollection");

    public static RouteHandlerBuilder MapUpdate(RouteGroupBuilder routes) => routes
        .MapPut("/collections/{id:guid}", async (Guid id, CollectionRequest request, ICurrentUser user, SaveKnowledgeCollection handler, CancellationToken ct) =>
            (await handler.UpdateAsync(user.Id, id, request, ct)).ToHttpResult())
        .WithName("UpdateKnowledgeCollection");

    public async Task<Result<CollectionDto>> CreateAsync(Guid actor, CollectionRequest request, CancellationToken ct)
    {
        var name = ResourceAccess.Name(request.Name);
        if (!name.IsSuccess) return name.Error;
        if (request.Description.Length > KnowledgeCollection.DescriptionMaxLength) return KnowledgeErrors.DescriptionTooLong;
        if (await db.Set<WorkspaceResource>().CountAsync(x => x.OwnerId == actor && x.Kind == KnowledgeCollection.Kind, ct) >= options.Value.MaxCollections)
            return KnowledgeErrors.CollectionLimit;
        var now = clock.GetUtcNow();
        var resource = new WorkspaceResource { OwnerId = actor, Kind = KnowledgeCollection.Kind, Name = name.Value, CreatedAt = now, UpdatedAt = now };
        db.Add(resource); db.Add(new KnowledgeCollection { Id = resource.Id, Description = request.Description.Trim() });
        db.AuditEvents.Add(new() { OwnerId = actor, ResourceId = resource.Id, Action = "knowledge.created", Result = "created" });
        await db.SaveChangesAsync(ct);
        return new CollectionDto(await access.DescribeAsync(actor, resource, ct), request.Description.Trim(), 0, 0);
    }

    public async Task<Result> UpdateAsync(Guid actor, Guid id, CollectionRequest request, CancellationToken ct)
    {
        var found = await access.RequireAsync(actor, id, KnowledgeCollection.Kind, ct, write: true);
        if (!found.IsSuccess) return found.Error;
        var resource = found.Value;
        var name = ResourceAccess.Name(request.Name);
        if (!name.IsSuccess) return name.Error;
        resource.Name = name.Value;
        if (request.Description.Length > KnowledgeCollection.DescriptionMaxLength) return KnowledgeErrors.DescriptionTooLong;
        (await db.Set<KnowledgeCollection>().SingleAsync(x => x.Id == id, ct)).Description = request.Description.Trim(); resource.UpdatedAt = clock.GetUtcNow();
        db.AuditEvents.Add(new() { OwnerId = actor, ResourceId = id, Action = "knowledge.updated", Result = "saved" });
        await db.SaveChangesAsync(ct);
        return Result.Success;
    }
}
