using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Modules;
using AiNexus.Platform.Validation;
using FluentValidation;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Work.Knowledge.Retrieval;

public sealed record AdminRetrievalSearchRequest(string Query, IReadOnlyList<Guid> CollectionIds, string? Mode = null, bool? Rerank = null);

/// <summary>與 <see cref="RetrievalPipeline.SearchAsync"/> 相同的查詢規則；模式與集合權限由 pipeline 之後再檢查。</summary>
internal sealed class AdminRetrievalSearchRequestValidator : RequestValidator<AdminRetrievalSearchRequest>
{
    public override Error Problem => KnowledgeErrors.QueryInvalid;

    public AdminRetrievalSearchRequestValidator()
    {
        RuleFor(x => x.Query).RequiredContent(2000);
    }
}

/// <summary>以管理者身分執行檢索管線，可覆寫模式與 rerank，用於管理端測試檢索結果。</summary>
internal sealed class TestAdminRetrieval(RetrievalPipeline pipeline) : IEndpointSlice
{
    public static void MapEndpoints(RouteGroupBuilder routes) => routes
        .MapPost("/search", (AdminRetrievalSearchRequest body, ICurrentUser user, TestAdminRetrieval handler, CancellationToken ct)
            => handler.HandleAsync(user.Id, body, ct))
        .Audited(KnowledgeAudit.RetrievalSearched)
        .WithName("TestAdminRetrieval");

    public Task<Result<KnowledgeSearchDto>> HandleAsync(Guid actor, AdminRetrievalSearchRequest request, CancellationToken ct)
        => pipeline.SearchAsync(actor, new(request.Query, request.CollectionIds), ct, mode: request.Mode, rerank: request.Rerank);
}
