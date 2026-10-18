using AiNexus.Features.Identity;
using AiNexus.Platform.Validation;
using FluentValidation;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Knowledge.Retrieval;

public sealed record AdminRetrievalSearchRequest(string Query, IReadOnlyList<Guid> CollectionIds, string? Mode = null, bool? Rerank = null);

/// <summary>與 <see cref="RetrievalPipeline.SearchAsync"/> 相同的查詢規則；模式與集合權限由 pipeline 之後再檢查。</summary>
internal sealed class AdminRetrievalSearchRequestValidator : RequestValidator<AdminRetrievalSearchRequest>
{
    public override string ProblemCode => "knowledge_query_invalid";

    public AdminRetrievalSearchRequestValidator()
    {
        RuleFor(x => x.Query).Must(x => !string.IsNullOrWhiteSpace(x) && x.Length <= 2000).WithErrorCode("length");
    }
}

/// <summary>以管理者身分執行檢索管線，可覆寫模式與 rerank，用於管理端測試檢索結果。</summary>
internal sealed class TestAdminRetrieval(RetrievalPipeline pipeline)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPost("/search", (AdminRetrievalSearchRequest body, ICurrentUser user, TestAdminRetrieval handler, CancellationToken ct)
            => handler.HandleAsync(user.Id, body, ct).ToHttpResultAsync())
        .WithName("TestAdminRetrieval");

    public Task<Result<KnowledgeSearchDto>> HandleAsync(Guid actor, AdminRetrievalSearchRequest request, CancellationToken ct)
        => pipeline.SearchAsync(actor, new(request.Query, request.CollectionIds), ct, mode: request.Mode, rerank: request.Rerank);
}
