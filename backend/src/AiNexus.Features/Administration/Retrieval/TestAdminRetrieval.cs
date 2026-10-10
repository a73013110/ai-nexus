using AiNexus.Features.Identity;
using AiNexus.Platform.Validation;
using FluentValidation;
using AiNexus.Features.Knowledge.Retrieval;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Administration.Retrieval;

public sealed record AdminRetrievalSearchRequest(string Query, IReadOnlyList<Guid> CollectionIds, string? Mode = null, bool? Rerank = null);

/// <summary>The query rule of <see cref="RetrievalPipeline.SearchAsync"/>, which also checks the mode and collection access afterwards.</summary>
internal sealed class AdminRetrievalSearchRequestValidator : RequestValidator<AdminRetrievalSearchRequest>
{
    public override string ProblemCode => "knowledge_query_invalid";

    public AdminRetrievalSearchRequestValidator()
    {
        RuleFor(x => x.Query).Must(x => !string.IsNullOrWhiteSpace(x) && x.Length <= 2000).WithErrorCode("length");
    }
}

/// <summary>Runs the retrieval pipeline as the administrator, with an optional mode and rerank override.</summary>
internal sealed class TestAdminRetrieval(RetrievalPipeline pipeline)
{
    public static void Map(RouteGroupBuilder routes) => routes
        .MapPost("/search", (AdminRetrievalSearchRequest body, ICurrentUser user, TestAdminRetrieval handler, CancellationToken ct) => handler.HandleAsync(user.Id, body, ct).ToHttpResultAsync())
        .WithName("TestAdminRetrieval");

    public Task<Result<KnowledgeSearchDto>> HandleAsync(Guid actor, AdminRetrievalSearchRequest request, CancellationToken ct)
        => pipeline.SearchAsync(actor, new(request.Query, request.CollectionIds), ct, mode: request.Mode, rerank: request.Rerank);
}
