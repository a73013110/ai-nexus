using AiNexus.Data.App;
using AiNexus.Data.App.Releases;
using AiNexus.Platform.Auditing;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Releases.Versions;

public sealed record DeploymentDto(int Id, string Component, string Version, string Revision, string? StartPoint, DateTimeOffset DeployedAt, string Host);

/// <summary>部署歷史，新的在前。一年發版數十次，固定取最近 <see cref="Limit"/> 筆，不分頁。</summary>
internal sealed class ListDeployments(AppDbContext db)
{
    public const int Limit = 200;

    public static void Map(RouteGroupBuilder routes) => routes
        .MapGet("/deployments", (ListDeployments handler, CancellationToken ct) => handler.HandleAsync(ct))
        .NotAudited("部署的版號、時間與主機名稱，不含結構內容")
        .WithName("ListDeployments");

    public async Task<IReadOnlyList<DeploymentDto>> HandleAsync(CancellationToken ct)
        => await db.Set<Deployment>().AsNoTracking().OrderByDescending(x => x.Id).Take(Limit)
            .Select(x => new DeploymentDto(x.Id, x.Component, x.Version, x.Revision, x.StartPoint, x.DeployedAt, x.Host)).ToListAsync(ct);
}
