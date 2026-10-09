using Microsoft.EntityFrameworkCore;
using AiNexus.Features.Quality.Feedback;
using AiNexus.Features.Quality.Evaluations;
using AiNexus.Features.Quality.RetrievalEvaluations;

namespace AiNexus.Features.Quality;

/// <summary>The module's tables, applied by <c>NexusDbContext</c>.</summary>
public static class QualityConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        model.ApplyConfiguration(new RetrievalEvaluationConfiguration());
        model.ApplyConfiguration(new RetrievalEvaluationResultConfiguration());
        model.ApplyConfiguration(new MessageFeedbackConfiguration());
        model.ApplyConfiguration(new EvaluationSetConfiguration());
        model.ApplyConfiguration(new EvaluationRunConfiguration());
        model.ApplyConfiguration(new EvaluationResultConfiguration());
    }
}
