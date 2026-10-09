using AiNexus.Features.Identity;
using AiNexus.Features.Persistence;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Repositories;

public sealed record ConnectRepositoryRequest(string Token);

internal sealed class ConnectRepositoryRequestValidator : RequestValidator<ConnectRepositoryRequest>
{
    public override string ProblemCode => "invalid_gitea_token";

    public ConnectRepositoryRequestValidator()
    {
        RuleFor(x => x.Token).Must(x => x is { Length: >= 20 and <= 512 } && x.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')).WithErrorCode("format");
    }
}

/// <summary>Verifies a personal access token against Gitea, then stores it encrypted for this user and host.</summary>
internal sealed class ConnectRepository(NexusDbContext db, RepositoryService gitea, IGiteaClient client, RepositoryWriteLock writes, TimeProvider clock)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("/connection", async (ConnectRepositoryRequest body, ICurrentUser user, ConnectRepository handler, CancellationToken ct) =>
            (await handler.HandleAsync(user.Id, body.Token, ct)).ToHttpResult())
        .WithName("ConnectRepository").Produces<RepositoryStatusDto>();

    public async Task<Result<RepositoryStatusDto>> HandleAsync(Guid owner, string token, CancellationToken ct)
    {
        if (!gitea.Enabled) return RepositoriesErrors.Disabled;
        using var identity = await client.GetAsync(token, "api/v1/user", ct);
        var login = RepositoryService.Text(identity.RootElement, "login", 100);
        if (login.Length == 0) throw new ApiException(502, "gitea_identity_invalid", "Gitea 未回傳帳號資訊。");
        await writes.Gate.WaitAsync(ct);
        try
        {
            var row = await db.Set<RepositoryConnection>().SingleOrDefaultAsync(x => x.OwnerId == owner, ct);
            if (row is null) { row = new() { OwnerId = owner }; db.Add(row); }
            row.BaseUrl = gitea.BaseUrl; row.Login = login; row.ProtectedToken = gitea.Protect(owner, token); row.ConnectedAt = clock.GetUtcNow();
            db.AuditEvents.Add(new AuditEvent { OwnerId = owner, ResourceId = owner, Action = "repository.connected", Result = "connected" });
            await db.SaveChangesAsync(ct);
            return await gitea.StatusAsync(owner, ct);
        }
        finally { writes.Gate.Release(); }
    }
}
