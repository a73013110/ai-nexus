using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Library;

internal sealed class ListPromptTemplates(NexusDbContext db)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("", async (ICurrentUser user, ListPromptTemplates handler, CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(user.Id, ct)))
        .WithName("ListPromptTemplates");

    public async Task<IReadOnlyList<PromptTemplateDto>> HandleAsync(Guid owner, CancellationToken ct)
    {
        var templates = await db.Set<PromptTemplate>().AsNoTracking().OwnedBy(owner)
            .OrderByDescending(x => x.UpdatedAt).Take(PromptTemplate.MaxPerOwner).ToListAsync(ct);
        return templates.Select(x => x.ToDto()).ToList();
    }
}
