using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Office.Flow;

/// <summary>
/// 申請種類（ADR 0036）：業務模組只決定「申請什麼」與「結束後做什麼」，狀態、指派、通知、代審與畫面都由引擎處理。
/// 以 <see cref="FlowRegistration.AddFlowKind{TKind}"/> 註冊；流程設定存在 <c>flow.FlowDefinitions</c>，每個種類要有種子（<c>database/App/Seed/flow.sql</c>）。
/// </summary>
public interface IFlowKind
{
    /// <summary>
    /// 申請結束（核准、退回、取消、逾期）時，在引擎的同一個交易內呼叫。回傳失敗時整筆決定回滾，決定的人會看到這個錯誤
    /// （例如儲存庫已停止開放申請）；只有核准要做事時，其他結果直接回成功。不可自行 <c>SaveChanges</c>。
    /// </summary>
    Task<Result> CompleteAsync(FlowCompletion completion, CancellationToken ct);
}

/// <summary>申請種類的宣告。<paramref name="Kind"/> 是穩定識別（kebab-case），寫進資料庫，改名等於新種類。</summary>
public sealed record FlowKindDefinition(string Kind, string Name, string Description)
{
    /// <summary>送出時提供、關卡條件可以引用的事實；條件只能是「事實等於其中一個值」。</summary>
    public IReadOnlyList<FlowFact> Facts { get; init; } = [];
}

/// <summary>關卡條件可引用的事實與它所有可能的值（設定畫面用下拉選單）。</summary>
public sealed record FlowFact(string Key, string Name, IReadOnlyList<FlowFactValue> Values);

public sealed record FlowFactValue(string Value, string Name);

/// <summary>結束的申請；<paramref name="Status"/> 是 <c>FlowStatuses</c> 之一，<paramref name="ActorId"/> 是做出最後決定的人（逾期為 null）。</summary>
public sealed record FlowCompletion(Guid InstanceId, string Kind, Guid SubjectId, Guid ApplicantId, string Status, Guid? ActorId);

public static class FlowRegistration
{
    /// <summary>註冊申請種類；鍵重複時 <see cref="KeyedServiceRegistration.VerifyKeyedServices"/> 在啟動時失敗。</summary>
    public static IServiceCollection AddFlowKind<TKind>(this IServiceCollection services, FlowKindDefinition definition) where TKind : class, IFlowKind
    {
        services.AddSingleton(definition);
        return services.AddKeyedService<IFlowKind, TKind>(definition.Kind, ServiceLifetime.Scoped);
    }

    /// <summary>註冊審核人規則；業務模組的規則以 <see cref="ApproverRuleDefinition.Kinds"/> 限定只給自己的種類用。</summary>
    public static IServiceCollection AddApproverRule<TRule>(this IServiceCollection services, ApproverRuleDefinition definition) where TRule : class, IApproverRule
    {
        services.AddSingleton(definition);
        return services.AddKeyedService<IApproverRule, TRule>(definition.Key, ServiceLifetime.Scoped);
    }
}
