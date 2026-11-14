using AiNexus.Platform.Errors;

namespace AiNexus.Features.Office.Flow;

/// <summary>
/// 審核人規則（ADR 0036）：關卡只存規則鍵與 JSON 參數，引擎呼叫對應的規則算出候選人。新增業務不必改引擎，註冊自己的規則即可。
/// </summary>
public interface IApproverRule
{
    /// <summary>儲存流程時檢查參數，有效回 null。參數只在這裡檢查，資料庫不認得 JSON 的內容。</summary>
    Task<Error?> ValidateAsync(string? argument, CancellationToken ct);

    /// <summary>
    /// 這一關的候選人。可以含申請人或已停用的帳號，引擎會排除；回空集合表示算不出人，引擎改交給簽核管理員。
    /// 參數已通過 <see cref="ValidateAsync"/>，但可能是舊版本的快照：讀不懂時回空集合，不要拋例外。
    /// </summary>
    Task<IReadOnlyCollection<Guid>> ResolveAsync(ApproverContext context, CancellationToken ct);
}

/// <summary>規則計算時可用的資料；業務規則以 <paramref name="SubjectId"/> 讀自己的申請內容。</summary>
public sealed record ApproverContext(string Kind, Guid SubjectId, Guid ApplicantId, string? Argument);

/// <summary>
/// 審核人規則的宣告。<paramref name="Key"/> 寫進流程設定與申請快照，改名等於新規則；
/// <paramref name="Argument"/> 是參數的形式（<see cref="ApproverArguments"/>），設定畫面據此選編輯器。
/// <paramref name="Description"/> 以 <c>\n</c> 分行：第一行是一句摘要（下拉選項的第二行），其後每行一個細節（選定後列在欄位下方）。
/// </summary>
public sealed record ApproverRuleDefinition(string Key, string Name, string Description, string Argument)
{
    /// <summary>只能用在這些申請種類；空的表示所有種類都能用。</summary>
    public IReadOnlyList<string> Kinds { get; init; } = [];

    public bool AllowedFor(string kind) => Kinds.Count == 0 || Kinds.Contains(kind, StringComparer.Ordinal);
}

/// <summary>審核人規則參數的形式；JSON 形狀由規則自己定義，這裡只告訴設定畫面要用哪種編輯器。</summary>
public static class ApproverArguments
{
    /// <summary>沒有參數（存 null）。</summary>
    public const string None = "none";

    /// <summary><c>{"levels":1}</c>：往上幾層。</summary>
    public const string Levels = "levels";

    /// <summary><c>{"userIds":["…"]}</c>：指定人員。</summary>
    public const string Users = "users";

    /// <summary><c>{"feature":"…"}</c>：一個可授予的功能 ID。</summary>
    public const string Feature = "feature";
}
