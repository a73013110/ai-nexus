using AiNexus.Features.Admin.Identity;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;

namespace AiNexus.Features.Work.Knowledge.Retrieval;

public sealed record KnowledgeSearchRequest(string Query, IReadOnlyList<Guid> CollectionIds);

/// <summary>
/// 查詢字串規則，是 <see cref="RetrievalPipeline.SearchAsync"/> 的第一道檢查。之後的知識庫數量上限、模式與權限檢查
/// 留在 pipeline 內，因為 pipeline 也服務此端點以外的呼叫者。
/// </summary>
internal sealed class KnowledgeSearchRequestValidator : RequestValidator<KnowledgeSearchRequest>
{
    public override string ProblemCode => "knowledge_query_invalid";

    public KnowledgeSearchRequestValidator()
    {
        RuleFor(x => x.Query).Must(x => !string.IsNullOrWhiteSpace(x) && x.Length <= 2000).WithErrorCode("length");
    }
}

/// <summary>在使用者可讀取的最多三個知識庫中搜尋，經過完整的檢索 pipeline。</summary>
internal sealed class SearchKnowledge(RetrievalPipeline pipeline)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("/search", (KnowledgeSearchRequest request, ICurrentUser user, SearchKnowledge handler, CancellationToken ct) => handler.HandleAsync(user.Id, request, ct).ToHttpResultAsync())
        .Audited(KnowledgeAudit.DocumentSearched)
        .WithName("SearchKnowledge");

    public Task<Result<KnowledgeSearchDto>> HandleAsync(Guid actor, KnowledgeSearchRequest request, CancellationToken ct) => pipeline.SearchAsync(actor, request, ct);
}
