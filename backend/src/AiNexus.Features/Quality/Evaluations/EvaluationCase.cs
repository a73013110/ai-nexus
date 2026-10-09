namespace AiNexus.Features.Quality.Evaluations;

public sealed record EvaluationCase(string Question, string Reference = "", IReadOnlyList<string>? RequiredTerms = null, IReadOnlyList<string>? ForbiddenTerms = null);
