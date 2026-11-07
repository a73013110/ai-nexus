using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Identity.ApiClients;

/// <summary>
/// 刪除外部系統，金鑰立即失效。過去的呼叫紀錄留在稽核（以系統 Id 為對象），不隨之刪除。
/// 會稽核（<c>identity.api_client.deleted</c>）。
/// </summary>
internal sealed class DeleteApiClient(AppDbContext db, AdministrativeWrite write) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapDelete("/{id:guid}", (Guid id, long rowVersion, DeleteApiClient handler, CancellationToken ct) => handler.HandleAsync(id, rowVersion, ct))
        .Audited(IdentityAudit.ApiClientDeleted)
        .WithName("DeleteApiClient");

    public async Task<Result> HandleAsync(Guid id, long rowVersion, CancellationToken ct)
    {
        var begun = await write.BeginAsync(ApiClientRules.Target(db, id), ct);
        if (!begun.IsSuccess) return begun.Error;
        await using var transaction = begun.Value;

        var client = await db.ApiClients.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (client is null) return IdentityErrors.ApiClientNotFound;
        db.ExpectRowVersion(client, rowVersion);
        db.Remove(client);

        return await transaction.CommitAsync(ct);
    }
}
