using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Library;

internal static class ListPromptTemplates
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("", HandleAsync)
        .WithName("ListPromptTemplates")
        .Produces<IReadOnlyList<PromptTemplateDto>>();

    private static async Task<IResult> HandleAsync(ICurrentUser user, NexusDbContext db, CancellationToken ct)
    {
        var templates = await db.Set<PromptTemplate>().AsNoTracking().OwnedBy(user.Id)
            .OrderByDescending(x => x.UpdatedAt).Take(PromptTemplate.MaxPerOwner).ToListAsync(ct);
        return TypedResults.Ok(templates.Select(x => x.ToDto()).ToList());
    }
}
