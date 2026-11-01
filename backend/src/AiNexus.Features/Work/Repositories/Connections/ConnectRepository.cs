using AiNexus.Data.App;
using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Work.Repositories.Connections;

public sealed record ConnectRepositoryRequest(string Token);

internal sealed class ConnectRepositoryRequestValidator : RequestValidator<ConnectRepositoryRequest>
{
    public override Error Problem => RepositoriesErrors.InvalidToken;

    public ConnectRepositoryRequestValidator()
    {
        RuleFor(x => x.Token).Must(x => x is { Length: >= 20 and <= 512 } && x.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')).WithErrorCode(RuleCode.Format);
    }
}

/// <summary>向 Gitea 驗證個人存取 token，然後以加密形式存給此使用者與主機。</summary>
internal sealed class ConnectRepository(AppDbContext db, RepositoryService gitea, IGiteaClient client, RepositoryWriteLock writes, TimeProvider clock)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("/connection", (ConnectRepositoryRequest body, ICurrentUser user, ConnectRepository handler, CancellationToken ct) =>
            handler.HandleAsync(user.Id, body.Token, ct))
        .Audited(RepositoriesAudit.ConnectionConnected)
        .WithName("ConnectRepository");

    public async Task<Result<RepositoryStatusDto>> HandleAsync(Guid owner, string token, CancellationToken ct)
    {
        if (!gitea.Enabled) return RepositoriesErrors.Disabled;
        using var identity = await client.GetAsync(token, "api/v1/user", ct);
        var login = GiteaJson.Text(identity.RootElement, "login", 100);
        if (login.Length == 0) return RepositoriesErrors.IdentityInvalid;
        // 網路驗證在鎖外進行，只有寫入資料列時才取鎖，避免佔住鎖等待 Gitea 回應。
        await writes.Gate.WaitAsync(ct);
        try
        {
            var row = await db.RepositoryConnections.SingleOrDefaultAsync(x => x.OwnerId == owner, ct);
            if (row is null) { row = new() { OwnerId = owner }; db.Add(row); }
            row.BaseUrl = gitea.BaseUrl; row.Login = login; row.ProtectedToken = gitea.Protect(owner, token); row.ConnectedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(ct);
            return await gitea.StatusAsync(owner, ct);
        }
        finally { writes.Gate.Release(); }
    }
}
