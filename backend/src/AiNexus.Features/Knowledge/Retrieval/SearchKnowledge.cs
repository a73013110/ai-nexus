using AiNexus.Features.Identity;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Validation;
using FluentValidation;

namespace AiNexus.Features.Knowledge.Retrieval;

public sealed record KnowledgeSearchRequest(string Query, IReadOnlyList<Guid> CollectionIds);

/// <summary>
/// The query rule of <see cref="RetrievalPipeline.SearchAsync"/>, its first check. The collection limit, mode and access
/// checks that follow stay in the pipeline, which also serves callers outside this endpoint.
/// </summary>
internal sealed class KnowledgeSearchRequestValidator : RequestValidator<KnowledgeSearchRequest>
{
    public override string ProblemCode => "knowledge_query_invalid";

    public KnowledgeSearchRequestValidator()
    {
        RuleFor(x => x.Query).Must(x => !string.IsNullOrWhiteSpace(x) && x.Length <= 2000).WithErrorCode("length");
    }
}

/// <summary>Searches up to three collections the user may read, through the full retrieval pipeline.</summary>
internal sealed class SearchKnowledge(RetrievalPipeline pipeline)
{
    public static RouteHandlerBuilder Map(RouteGroupBuilder routes) => routes
        .MapPost("/search", (KnowledgeSearchRequest request, ICurrentUser user, SearchKnowledge handler, CancellationToken ct) => handler.HandleAsync(user.Id, request, ct).ToHttpResultAsync())
        .WithName("SearchKnowledge");

    public Task<Result<KnowledgeSearchDto>> HandleAsync(Guid actor, KnowledgeSearchRequest request, CancellationToken ct) => pipeline.SearchAsync(actor, request, ct);
}
