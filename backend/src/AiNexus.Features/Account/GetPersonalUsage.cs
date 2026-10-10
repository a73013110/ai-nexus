using AiNexus.Features.Identity;

namespace AiNexus.Features.Account;

internal sealed class GetPersonalUsage(PersonalUsage usage)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapGet("/settings/usage", async (ICurrentUser user, GetPersonalUsage handler, CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(user.Id, ct)))
        .WithName("GetPersonalUsage");

    public Task<PersonalUsageDto> HandleAsync(Guid owner, CancellationToken ct) => usage.ForOwnerAsync(owner, ct);
}
