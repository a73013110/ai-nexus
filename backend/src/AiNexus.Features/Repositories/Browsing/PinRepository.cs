using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Repositories.Browsing;

/// <summary>Keeps a repository the user can read at the top of the list, with the name and description Gitea shows now.</summary>
internal sealed class PinRepository(NexusDbContext db, RepositoryService gitea, RepositoryWriteLock writes)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder repository) => repository
        .MapPut("/pin", (string owner, string name, ICurrentUser user, PinRepository handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, owner + "/" + name, ct).ToHttpResultAsync())
        .WithName("PinRepository");

    public async Task<Result<RepositoryPinDto>> HandleAsync(Guid owner, string repo, CancellationToken ct)
    {
        if (!RepositoryService.IsRepository(repo)) return RepositoriesErrors.InvalidRepository;
        var read = await gitea.GetAsync(owner, RepositoryService.Route(repo), ct);
        if (!read.IsSuccess) return read.Error!;
        string name, description; bool isPrivate;
        using (var json = read.Value!)
        {
            name = GiteaJson.Text(json.RootElement, "full_name", 201);
            description = GiteaJson.Text(json.RootElement, "description", 500);
            isPrivate = GiteaJson.Flag(json.RootElement, "private");
        }
        if (!RepositoryService.IsRepository(name)) throw new ExternalServiceException(RepositoriesErrors.ResponseInvalid);
        await writes.Gate.WaitAsync(ct);
        try
        {
            var pins = db.Set<RepositoryPin>().Where(x => x.OwnerId == owner && x.BaseUrl == gitea.BaseUrl);
            var row = await pins.SingleOrDefaultAsync(x => x.Repository == name, ct);
            if (row is null)
            {
                if (await pins.CountAsync(ct) >= RepositoryPin.Limit) return RepositoriesErrors.PinLimit;
                row = new RepositoryPin { OwnerId = owner, BaseUrl = gitea.BaseUrl, Repository = name };
                db.Add(row);
            }
            row.Description = description; row.Private = isPrivate;
            await db.SaveChangesAsync(ct);
            return new RepositoryPinDto(row.Repository, row.Description, row.Private, row.PinnedAt);
        }
        finally { writes.Gate.Release(); }
    }
}
