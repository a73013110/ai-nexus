using AiNexus.Features.Platform.Inference.ModelTasks;

namespace AiNexus.Features.Admin.Quality.Evaluations;

public sealed record EvaluationVariant(string Label, string ModelId, string Instruction, ModelTaskSnapshot? Configuration = null, string? ModelDisplayName = null);
