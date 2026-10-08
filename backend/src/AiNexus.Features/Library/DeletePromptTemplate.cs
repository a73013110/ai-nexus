using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Library;

internal static class DeletePromptTemplate
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapDelete("/{id:guid}", HandleAsync)
        .WithName("DeletePromptTemplate")
        .Produces(StatusCodes.Status204NoContent);

    private static async Task<IResult> HandleAsync(Guid id, ICurrentUser user, NexusDbContext db, CancellationToken ct)
    {
        var deleted = await db.Set<PromptTemplate>().OwnedBy(user.Id).Where(x => x.Id == id).ExecuteDeleteAsync(ct);
        return deleted == 0 ? LibraryErrors.NotFound.ToProblem() : TypedResults.NoContent();
    }
}
