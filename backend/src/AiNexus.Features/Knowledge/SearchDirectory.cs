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
internal static class SearchDirectory
{
    /// <summary>Up to 20 accounts matching 2 to 120 characters of account or display name; anything else finds no one.</summary>
    public static RouteHandlerBuilder MapUsers(RouteGroupBuilder api) => api
        .MapGet("/directory", async (string search, ICurrentUser user, NexusDbContext db, CancellationToken ct) =>
        {
            if (search.Trim().Length is < 2 or > 120) return Results.Ok(Array.Empty<DirectoryUserDto>());
            return Results.Ok(await db.Users.AsNoTracking().Where(x => x.Account.Contains(search.Trim()) || x.DisplayName.Contains(search.Trim())).OrderBy(x => x.Account).Take(20).Select(x => new DirectoryUserDto(x.Id, x.Account, x.DisplayName)).ToListAsync(ct));
        })
        .WithName("SearchUserDirectory").Produces<IReadOnlyList<DirectoryUserDto>>();

    public static RouteHandlerBuilder MapGroups(RouteGroupBuilder api) => api
        .MapGet("/directory/groups", async (ICurrentUser user, NexusDbContext db, CancellationToken ct) =>
            Results.Ok(await db.Set<RoleGroup>().AsNoTracking().Where(x => x.Enabled).OrderBy(x => x.Name).Select(x => new DirectoryGroupDto(x.Id, x.Name)).ToListAsync(ct)))
        .WithName("ListDirectoryGroups").Produces<IReadOnlyList<DirectoryGroupDto>>();
}
