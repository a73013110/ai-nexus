using AiNexus.Features.Identity;

namespace AiNexus.Features.Account;

internal static class GetPersonalUsage
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/settings/usage", async (ICurrentUser user, PersonalUsage usage, CancellationToken ct) => Results.Ok(await usage.ForOwnerAsync(user.Id, ct)))
        .WithName("GetPersonalUsage").Produces<PersonalUsageDto>();
}
