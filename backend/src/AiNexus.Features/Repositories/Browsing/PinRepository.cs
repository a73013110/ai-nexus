using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Repositories.Browsing;

/// <summary>把使用者可讀取的程式庫釘在清單頂端，並同步為 Gitea 目前的名稱與說明。</summary>
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
        // 釘選數量上限是先計數再新增，兩個並行請求可能同時通過檢查，所以用寫入鎖序列化。
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
