using System.Security.Cryptography;
using System.Text.Json;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Inference;

// Snapshot answer-affecting settings without exposing model identifiers, endpoints or credentials.
public sealed record ModelTaskSnapshot(int ContextTokens, int MaxOutputTokens, double Temperature, string ReasoningEffort, string Fingerprint);

public static class ModelTaskConfiguration
{
    public const double Temperature = .2;
    public static ModelTaskSnapshot Capture(ModelProfile profile, InferenceOptions options)
    {
        var provider = profile.Provider;
        var serialized = JsonSerializer.SerializeToUtf8Bytes(new {
            Version = 1, Provider = provider,
            Endpoint = provider == "ollama" ? options.Providers.Ollama.Endpoint.TrimEnd('/') : "google-v1beta",
            profile.Id, profile.NativeId, profile.ContextTokens, profile.MaxOutputTokens, Temperature,
            profile.DefaultReasoningEffort, profile.ReasoningControl, profile.SupportsImages
        });
        return new(profile.ContextTokens, profile.MaxOutputTokens, Temperature, profile.DefaultReasoningEffort, Convert.ToHexString(SHA256.HashData(serialized)));
    }
    /// <summary>A queued evaluation or review runs only with the model configuration it was created with.</summary>
    public static Result Require(ModelProfile profile, InferenceOptions options, string? expected)
        => expected is not null && StringComparer.Ordinal.Equals(expected, Capture(profile, options).Fingerprint) ? Result.Success : InferenceErrors.ConfigurationChanged;
}
