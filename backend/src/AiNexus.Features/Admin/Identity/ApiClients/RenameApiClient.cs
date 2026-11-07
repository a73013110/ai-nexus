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

public sealed record ApiClientRenameRequest(string Name, long RowVersion);

internal sealed class ApiClientRenameRequestValidator : RequestValidator<ApiClientRenameRequest>
{
    public ApiClientRenameRequestValidator()
    {
        RuleFor(x => x.Name).RequiredText(ApiClient.NameMaxLength);
        RuleFor(x => x.RowVersion).GreaterThan(0).WithErrorCode(RuleCode.Required);
    }
}

/// <summary>修改外部系統的名稱；金鑰不變，對方系統不必改設定。會稽核（<c>identity.api_client.renamed</c>）。</summary>
internal sealed class RenameApiClient(AppDbContext db, AdministrativeWrite write) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapPut("/{id:guid}", (Guid id, ApiClientRenameRequest body, RenameApiClient handler, CancellationToken ct) => handler.HandleAsync(id, body, ct))
        .Audited(IdentityAudit.ApiClientRenamed)
        .WithName("RenameApiClient");

    public async Task<Result> HandleAsync(Guid id, ApiClientRenameRequest request, CancellationToken ct)
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

        return await transaction.CommitAsync(ct);
    }
}
