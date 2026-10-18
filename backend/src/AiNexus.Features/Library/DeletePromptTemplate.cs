using AiNexus.Data.App;
using AiNexus.Data.App.Library;
using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Library;

internal sealed class DeletePromptTemplate(AppDbContext db)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapDelete("/{id:guid}", (Guid id, ICurrentUser user, DeletePromptTemplate handler, CancellationToken ct) => handler.HandleAsync(user.Id, id, ct).ToHttpResultAsync())
        .WithName("DeletePromptTemplate");

    public async Task<Result> HandleAsync(Guid owner, Guid id, CancellationToken ct)
    {
        var deleted = await db.Set<PromptTemplate>().OwnedBy(owner).Where(x => x.Id == id).ExecuteDeleteAsync(ct);
        return deleted == 0 ? LibraryErrors.NotFound : Result.Success;
    }
}
