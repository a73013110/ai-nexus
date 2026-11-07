using AiNexus.Data.App;
using AiNexus.Data.App.Identity;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Admin.Identity.ApiClients;

/// <summary>外部系統的名稱、聯絡人與是否啟用；停用與啟用也走這裡，稽核的前後快照看得出改了哪一項。</summary>
public sealed record ApiClientUpdateRequest(string Name, string? Contact, bool Enabled, long RowVersion);

internal sealed class ApiClientUpdateRequestValidator : RequestValidator<ApiClientUpdateRequest>
{
    public ApiClientUpdateRequestValidator()
    {
        RuleFor(x => x.Name).RequiredText(ApiClient.NameMaxLength);
        RuleFor(x => x.Contact).OptionalText(ApiClient.ContactMaxLength);
        RuleFor(x => x.RowVersion).GreaterThan(0).WithErrorCode(RuleCode.Required);
    }
}

/// <summary>
/// 修改外部系統；金鑰不變，對方系統不必改設定。停用後金鑰的呼叫一律回 401，直到重新啟用。
/// 會稽核（<c>identity.api_client.updated</c>）。
/// </summary>
internal sealed class UpdateApiClient(AppDbContext db, AdministrativeWrite write) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapPut("/{id:guid}", (Guid id, ApiClientUpdateRequest body, UpdateApiClient handler, CancellationToken ct) => handler.HandleAsync(id, body, ct))
        .Audited(IdentityAudit.ApiClientUpdated)
        .WithName("UpdateApiClient");

    public async Task<Result> HandleAsync(Guid id, ApiClientUpdateRequest request, CancellationToken ct)
    {
        var begun = await write.BeginAsync(ApiClientRules.Target(db, id), ct);
        if (!begun.IsSuccess) return begun.Error;
        await using var transaction = begun.Value;

        var client = await db.ApiClients.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (client is null) return IdentityErrors.ApiClientNotFound;
        var name = request.Name.Trim();
        if (await ApiClientRules.NameTakenAsync(db, name, id, ct)) return IdentityErrors.ApiClientNameTaken;
        db.ExpectRowVersion(client, request.RowVersion);
        client.Name = name;
        client.Contact = ApiClientRules.Contact(request.Contact);
        client.Enabled = request.Enabled;

        return await transaction.CommitAsync(ct);
    }
}
