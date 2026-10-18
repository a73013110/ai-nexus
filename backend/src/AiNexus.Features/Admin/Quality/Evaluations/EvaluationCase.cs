namespace AiNexus.Features.Admin.Quality.Evaluations;

public sealed record EvaluationCase(string Question, string Reference = "", IReadOnlyList<string>? RequiredTerms = null, IReadOnlyList<string>? ForbiddenTerms = null);
