using AiNexus.Data.App;
using AiNexus.Data.App.Prompts;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Prompts;

internal sealed class ListPromptTemplates(AppDbContext db) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapGet("", (ICurrentUser user, ListPromptTemplates handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, ct))
        .NotAudited("輸入框的範本選單")
        .WithName("ListPromptTemplates");

    public async Task<IReadOnlyList<PromptTemplateDto>> HandleAsync(Guid owner, CancellationToken ct)
    {
        var templates = await db.PromptTemplates.AsNoTracking().OwnedBy(owner)
            .OrderByDescending(x => x.UpdatedAt).Take(PromptTemplate.MaxPerOwner).ToListAsync(ct);
        return templates.Select(x => x.ToDto()).ToList();
    }
}
