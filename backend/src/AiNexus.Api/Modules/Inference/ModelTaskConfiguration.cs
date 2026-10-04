using System.Security.Cryptography;
using System.Text.Json;
using AiNexus.BuildingBlocks;

namespace AiNexus.Modules.Inference;

// Snapshot answer-affecting settings without exposing model identifiers, endpoints or credentials.
public sealed record ModelTaskSnapshot(int ContextTokens, int MaxOutputTokens, double Temperature, string ReasoningEffort, string Fingerprint);

public static class ModelTaskConfiguration
{
    public const double Temperature = .2;
    public static ModelTaskSnapshot Capture(ModelProfile profile, InferenceOptions options)
    {
        var provider = options.Provider.Trim().ToLowerInvariant();
        var serialized = JsonSerializer.SerializeToUtf8Bytes(new {
            Version = 1, Provider = provider,
            Endpoint = provider == "ollama" ? options.BaseUrl.TrimEnd('/') : "google-v1beta",
            profile.Id, profile.ContextTokens, profile.MaxOutputTokens, Temperature,
            profile.DefaultReasoningEffort, profile.ReasoningControl, profile.SupportsImages
        });
        return new(profile.ContextTokens, profile.MaxOutputTokens, Temperature, profile.DefaultReasoningEffort, Convert.ToHexString(SHA256.HashData(serialized)));
    }
    public static void Require(ModelProfile profile, InferenceOptions options, string? expected)
    {
        if (expected is null || !StringComparer.Ordinal.Equals(expected, Capture(profile, options).Fingerprint))
            throw new ApiException(409, "evaluation_configuration_changed", "模型設定已變更；請建立新的比較，避免混用不同設定的結果。");
    }
}
