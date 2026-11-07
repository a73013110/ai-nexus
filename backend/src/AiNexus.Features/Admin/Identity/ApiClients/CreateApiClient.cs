using AiNexus.Data.App;
using AiNexus.Data.App.Identity;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Validation;
using FluentValidation;

namespace AiNexus.Features.Admin.Identity.ApiClients;

/// <summary>新增外部系統；名稱是給管理員辨認的系統名稱，例如「公文系統」。</summary>
public sealed record ApiClientCreateRequest(string Name);

internal sealed class ApiClientCreateRequestValidator : RequestValidator<ApiClientCreateRequest>
{
    public ApiClientCreateRequestValidator() => RuleFor(x => x.Name).RequiredText(ApiClient.NameMaxLength);
}

/// <summary>新增外部系統並產生它的金鑰；金鑰只在這次回應出現。會稽核（<c>identity.api_client.created</c>）。</summary>
internal sealed class CreateApiClient(AppDbContext db, AdministrativeWrite write, TimeProvider clock) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapPost("", (ApiClientCreateRequest body, CreateApiClient handler, CancellationToken ct) => handler.HandleAsync(body, ct))
        .Audited(IdentityAudit.ApiClientCreated)
        .WithName("CreateApiClient");

    public async Task<Result<IssuedApiClientKeyDto>> HandleAsync(ApiClientCreateRequest request, CancellationToken ct)
    {
        var key = ApiClientKeys.Issue();
        var client = new ApiClient { Name = request.Name.Trim(), KeyHash = key.Hash, KeyHint = key.Hint, KeyIssuedAt = clock.GetUtcNow() };
        var begun = await write.BeginAsync(ApiClientRules.Target(db, client.Id), ct);
        if (!begun.IsSuccess) return begun.Error;
        await using var transaction = begun.Value;

        if (await ApiClientRules.NameTakenAsync(db, client.Name, client.Id, ct)) return IdentityErrors.ApiClientNameTaken;
        db.Add(client);

        return await transaction.CommitAsync(() => new IssuedApiClientKeyDto(
            new(client.Id, client.RowVersion, client.Name, client.KeyHint, client.KeyIssuedAt, client.CreatedAt, null), key.Key), ct);
    }
}
