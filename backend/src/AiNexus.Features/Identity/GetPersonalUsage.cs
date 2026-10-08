using AiNexus.Features.Inference;

namespace AiNexus.Features.Identity;

internal static class GetPersonalUsage
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/settings/usage", async (ICurrentUser user, UsageReports reports, CancellationToken ct) => Results.Ok(await reports.ForOwnerAsync(user.Id, ct)))
        .WithName("GetPersonalUsage").Produces<PersonalUsageDto>();
}
