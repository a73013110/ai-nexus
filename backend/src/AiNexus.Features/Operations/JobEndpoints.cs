using AiNexus.Features.Identity;

namespace AiNexus.Features.Operations;

public static class JobEndpoints
{
    public static void MapJobs(this RouteGroupBuilder api)
    {
        var routes = api.MapGroup("/jobs").WithTags("Background jobs");
        routes.MapGet("", async (CurrentUser current, JobService jobs, CancellationToken ct) => await jobs.ListAsync((await current.GetAsync(ct)).Id, ct)).WithName("ListJobs").Produces<IReadOnlyList<JobDto>>();
        routes.MapGet("/{id:guid}", async (Guid id, CurrentUser current, JobService jobs, CancellationToken ct) => JobService.Describe(await jobs.OwnedAsync((await current.GetAsync(ct)).Id, id, ct))).WithName("GetJob").Produces<JobDto>();
        routes.MapPost("/{id:guid}/cancel", async (Guid id, CurrentUser current, JobService jobs, CancellationToken ct) => await jobs.CancelAsync((await current.GetAsync(ct)).Id, id, ct)).WithName("CancelJob").Produces<JobDto>();
        routes.MapPost("/{id:guid}/retry", async (Guid id, CurrentUser current, JobService jobs, CancellationToken ct) => await jobs.RetryAsync((await current.GetAsync(ct)).Id, id, ct)).WithName("RetryJob").Produces<JobDto>();
    }
}
