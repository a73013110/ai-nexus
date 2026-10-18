using AiNexus.Features.AccessControl;

namespace AiNexus.Features.Operations;

/// <summary>
/// 平台營運：平台用量、檢索管理、模型與工具價格，以及全平台總覽。它有自己的授權，與存取管理（<see cref="FeatureIds.Admin"/>）分開。
/// 其他模組的營運端點直接引用 <see cref="FeatureIds.Operations"/>／<see cref="Policies.Operations"/>，不依賴這個模組。
/// </summary>
public static class OperationsConfiguration
{
    public const string Feature = FeatureIds.Operations, Policy = Policies.Operations;
}
