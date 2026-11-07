using AiNexus.Data.App;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Identity.ApiClients;

public sealed record ApiClientKeyRequest(long RowVersion);

internal sealed class ApiClientKeyRequestValidator : RequestValidator<ApiClientKeyRequest>
{
    public ApiClientKeyRequestValidator() => RuleFor(x => x.RowVersion).GreaterThan(0).WithErrorCode(RuleCode.Required);
}

/// <summary>
/// 重新產生金鑰（外洩或定期更換）：舊金鑰在提交的同時失效，不保留重疊期——外洩時要的是立刻失效，
/// 一般更換則請對方先準備好、再按下產生並馬上貼上新金鑰。帶版本：兩位管理員同時產生時，後到的不會讓先拿到的金鑰無聲失效。
/// 會稽核（<c>identity.api_client_key.created</c>）。
/// </summary>
internal sealed class ReissueApiClientKey(AppDbContext db, AdministrativeWrite write, TimeProvider clock) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapPost("/{id:guid}/keys", (Guid id, ApiClientKeyRequest body, ReissueApiClientKey handler, CancellationToken ct) => handler.HandleAsync(id, body, ct))
        .Audited(IdentityAudit.ApiClientKeyReissued)
        .WithName("ReissueApiClientKey");

    public async Task<Result<IssuedApiClientKeyDto>> HandleAsync(Guid id, ApiClientKeyRequest request, CancellationToken ct)
    {
        var key = ApiClientKeys.Issue();
        var begun = await write.BeginAsync(ApiClientRules.Target(db, id), ct);
        if (!begun.IsSuccess) return begun.Error;
        await using var transaction = begun.Value;

        var client = await db.ApiClients.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (client is null) return IdentityErrors.ApiClientNotFound;
        db.ExpectRowVersion(client, request.RowVersion);
        client.KeyHash = key.Hash;
        client.KeyHint = key.Hint;
        client.KeyIssuedAt = clock.GetUtcNow();

        var committed = await transaction.CommitAsync(ct);
        if (!committed.IsSuccess) return committed.Error;
        return new IssuedApiClientKeyDto(await ApiClientDto.Project(db, db.ApiClients.AsNoTracking().Where(x => x.Id == id)).SingleAsync(ct), key.Key);
    }
}
