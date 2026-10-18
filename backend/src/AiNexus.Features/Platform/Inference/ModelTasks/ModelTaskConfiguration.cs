using System.Security.Cryptography;
using System.Text.Json;
using AiNexus.Features.Platform.Inference.Models;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Platform.Inference.ModelTasks;

/// <summary>
/// 模型任務執行組態快照。
/// 記錄影響生成結果的設定（不暴露端點、金鑰或敏感憑證），並提供 SHA-256 雜湊指紋。
/// </summary>
public sealed record ModelTaskSnapshot(
    int ContextTokens,
    int MaxOutputTokens,
    double Temperature,
    string ReasoningEffort,
    string Fingerprint);

/// <summary>
/// 模型非交談任務組態工具。
/// 用於計算與比對推論參數指紋，確保排隊中的評估或審查任務在執行時維持原始組態不變。
/// </summary>
public static class ModelTaskConfiguration
{
    /// <summary>文字轉換、OCR 等任務採用的標準低隨機性溫度（0.2）。</summary>
    public const double Temperature = .2;

    /// <summary>
    /// 擷取當前模型與服務組態的不可變快照與指紋。
    /// </summary>
    public static ModelTaskSnapshot Capture(ModelDefinition profile, InferenceOptions options)
    {
        var provider = profile.Provider;
        var serialized = JsonSerializer.SerializeToUtf8Bytes(new
        {
            Version = 1,
            Provider = provider,
            Endpoint = provider == "ollama" ? options.Providers.Ollama.Endpoint.TrimEnd('/') : "google-v1beta",
            profile.Id,
            profile.NativeId,
            profile.ContextTokens,
            profile.MaxOutputTokens,
            Temperature,
            profile.DefaultReasoningEffort,
            profile.ReasoningControl,
            profile.SupportsImages
        });

        return new(
            profile.ContextTokens,
            profile.MaxOutputTokens,
            Temperature,
            profile.DefaultReasoningEffort,
            Convert.ToHexString(SHA256.HashData(serialized)));
    }

    /// <summary>
    /// 驗證排隊任務的模型組態是否與建立當下完全一致。
    /// 若管理員在此期間修改了端點或模型參數，則拒絕執行以防評測結果失真。
    /// </summary>
    public static Result Require(ModelDefinition profile, InferenceOptions options, string? expected) =>
        expected is not null && StringComparer.Ordinal.Equals(expected, Capture(profile, options).Fingerprint)
            ? Result.Success
            : InferenceErrors.ConfigurationChanged;
}
