using System.Text.Json;
using AiNexus.Data.App;
using AiNexus.Data.App.Flow;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Office.Flow;

/// <summary>
/// 送出一筆申請。<paramref name="SubjectId"/> 是業務資料（申請內容）的 ID，每筆業務資料只能送出一次；
/// <paramref name="Title"/> 與 <see cref="Details"/> 是給通用畫面與通知顯示的快照，<see cref="Facts"/> 給關卡條件判斷。
/// </summary>
public sealed record FlowSubmission(string Kind, Guid SubjectId, Guid ApplicantId, string Title)
{
    /// <summary>鍵與值都必須是種類宣告過的（<see cref="FlowKindDefinition.Facts"/>），否則是程式錯誤。</summary>
    public IReadOnlyDictionary<string, string> Facts { get; init; } = new Dictionary<string, string>();

    public IReadOnlyList<FlowDetail> Details { get; init; } = [];
}

/// <summary>申請詳情的一列（標籤、值），依送出時的順序顯示。</summary>
public sealed record FlowDetail(string Label, string Value)
{
    internal static string Serialize(IReadOnlyList<FlowDetail> details) => JsonSerializer.Serialize(details, JsonSerializerOptions.Web);

    /// <summary>快照由引擎寫入；讀不懂時（手動改壞）回空清單，畫面仍可顯示其他資訊。</summary>
    internal static IReadOnlyList<FlowDetail> Parse(string json)
    {
        try { return JsonSerializer.Deserialize<FlowDetail[]>(json, JsonSerializerOptions.Web) ?? []; }
        catch (JsonException) { return []; }
    }
}

/// <summary>
/// 業務模組使用簽核的入口（ADR 0036）。狀態只在 <c>flow.FlowInstances</c>：業務要判斷「是否有進行中的申請」時，
/// 把這裡的可組合查詢放進自己的 SQL，不在業務表另存狀態。
/// </summary>
[Scoped]
public sealed class FlowService(AppDbContext db, FlowEngine engine)
{
    /// <summary>
    /// 建立申請並啟動第一個適用的關卡；沒有適用的關卡時直接核准（同一交易內呼叫 <see cref="IFlowKind.CompleteAsync"/>）。
    /// 只加進 ChangeTracker，由呼叫端與業務資料一起儲存。種類沒有流程設定時回 <c>flow_definition_missing</c>。
    /// </summary>
    public async Task<Result<Guid>> SubmitAsync(FlowSubmission submission, CancellationToken ct)
    {
        var started = await engine.StartAsync(submission, ct);
        return started.IsSuccess ? started.Value.Id : started.Error;
    }

    /// <summary>種類 <paramref name="kind"/> 審核中的申請所對應的業務資料 ID。</summary>
    public IQueryable<Guid> PendingSubjects(string kind)
        => db.FlowInstances.Where(x => x.Kind == kind && x.Status == FlowStatuses.Pending).Select(x => x.SubjectId);

    /// <summary>種類 <paramref name="kind"/> 的所有申請（不追蹤），給業務頁面顯示狀態。</summary>
    public IQueryable<FlowInstance> Instances(string kind) => db.FlowInstances.AsNoTracking().Where(x => x.Kind == kind);
}
