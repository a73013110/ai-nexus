using System.Text.Json;

namespace AiNexus.Features.Quality.Evaluations;

/// <summary>Frozen JSON columns of sets and runs (default serializer options, as stored).</summary>
internal static class EvaluationJson
{
    public static T[] Parse<T>(string json) => JsonSerializer.Deserialize<T[]>(json)!;
}
