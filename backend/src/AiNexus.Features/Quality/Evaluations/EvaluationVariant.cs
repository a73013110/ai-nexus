using AiNexus.Features.Inference.ModelTasks;

namespace AiNexus.Features.Quality.Evaluations;

public sealed record EvaluationVariant(string Label, string ModelId, string Instruction, ModelTaskSnapshot? Configuration = null, string? ModelDisplayName = null);
