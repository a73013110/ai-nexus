using AiNexus.Features.AccessControl;
using AiNexus.Features.Collaboration;
using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Knowledge;

/// <summary>
/// People and enabled groups a signed-in user can name when sharing a resource. The <see cref="ICurrentUser"/>
/// parameter makes the request filter resolve (and so admit) the caller before anything is read.
/// </summary>
internal sealed class SearchDirectory(NexusDbContext db)
{
    public static RouteHandlerBuilder MapUsers(RouteGroupBuilder api) => api
        .MapGet("/directory", async (string search, ICurrentUser user, SearchDirectory handler, CancellationToken ct) => TypedResults.Ok(await handler.UsersAsync(search, ct)))
        .WithName("SearchUserDirectory");

    public static RouteHandlerBuilder MapGroups(RouteGroupBuilder api) => api
        .MapGet("/directory/groups", async (ICurrentUser user, SearchDirectory handler, CancellationToken ct) => TypedResults.Ok(await handler.GroupsAsync(ct)))
        .WithName("ListDirectoryGroups");

    /// <summary>Up to 20 accounts matching 2 to 120 characters of account or display name; anything else finds no one.</summary>
    public async Task<IReadOnlyList<DirectoryUserDto>> UsersAsync(string search, CancellationToken ct)
    {
        var term = search.Trim();
        if (term.Length is < 2 or > 120) return [];
        return await db.Users.AsNoTracking().Where(x => x.Account.Contains(term) || x.DisplayName.Contains(term)).OrderBy(x => x.Account).Take(20).Select(x => new DirectoryUserDto(x.Id, x.Account, x.DisplayName)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DirectoryGroupDto>> GroupsAsync(CancellationToken ct)
        => await db.Set<RoleGroup>().AsNoTracking().Where(x => x.Enabled).OrderBy(x => x.Name).Select(x => new DirectoryGroupDto(x.Id, x.Name)).ToListAsync(ct);
}
