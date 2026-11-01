namespace AiNexus.Platform.Modules;

/// <summary>
/// 標在 <c>static partial</c> 方法上，<c>FieldCatalogGenerator</c> 在編譯期實作它：回傳組件內所有 static 類別裡、型別與回傳的元素型別相同的
/// public static 欄位，依類別完整名稱、再依宣告順序排列。各模組各自宣告、集中列出的清單（例如稽核動作）用它取代執行期掃描組件（ADR 0030）。
/// 方法沒有參數、回傳 <c>T[]</c> 或 <c>IReadOnlyList&lt;T&gt;</c>，宣告在頂層、非泛型的 partial 類別。
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class FieldCatalogAttribute : Attribute;
