using System.Text.Json;
using AiNexus.Platform.Auditing;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Platform.Assistant;

/// <summary>
/// 助理可以呼叫的一個工具（概念同 MCP 的 tool，在程式內執行）。模組實作這個介面就完成登記（組合產生器註冊），
/// 全站助理（<see cref="AskAssistant"/>）只把提問者持有 <see cref="Feature"/> 的工具交給模型。
/// 工具以提問者的身分與資料範圍執行；數字與判定由工具算好寫成事實，模型只負責轉述。
/// </summary>
[RegisterImplementations(typeof(AssistantToolRegistration), nameof(AssistantToolRegistration.AddAssistantTool))]
public interface IAssistantTool
{
    /// <summary>給模型的名稱：小寫 snake_case、全站唯一，以模組開頭（<c>attendance_records</c>）。</summary>
    string Name { get; }

    /// <summary>畫面顯示的動作（「查詢出勤紀錄」）：回答途中讓使用者知道助理在查什麼。</summary>
    string Title { get; }

    /// <summary>給模型的說明：查得到什麼、什麼時候用、參數不填代表什麼。</summary>
    string Description { get; }

    /// <summary>持有這個功能（<c>FeatureCatalog</c> 的 ID）才交給模型。</summary>
    string Feature { get; }

    /// <summary>
    /// 這個領域的回答規則（名詞的意思、該用哪個工具），寫進系統指令；同一模組的工具共用同一段，相同的只寫一次。
    /// 判定與列出的條件不寫在這裡，寫成結果裡的〈規則〉事實（<see cref="AssistantFact.Rule"/>），模型才能引用、畫面才看得到。
    /// </summary>
    string Guidance { get; }

    /// <summary>每次呼叫寫一筆的稽核動作；null 表示讀的不是需要留證的資料（例如規則說明）。</summary>
    AuditAction? Audit { get; }

    AssistantParameters Parameters { get; }

    /// <summary>
    /// 是不是 <see cref="Feature"/> 那一頁「畫面上的資料」：在那一頁提問時先以空參數執行一次（條件取自 <see cref="AssistantToolCall.View"/>），
    /// 所以常見的問題一輪就能回答，不支援工具的模型也看得到這一頁。每頁最多一個。
    /// </summary>
    bool DescribesPage => false;

    Task<Result<AssistantToolResult>> InvokeAsync(AssistantToolCall call, CancellationToken ct);
}

/// <summary>
/// 一次工具呼叫。<paramref name="View"/> 是提問所在頁面目前的查詢條件（頁面送來的 JSON，只有工具屬於那一頁時才有值），
/// 工具在參數沒填時以它為準；內容來自瀏覽器，工具要和頁面的查詢端點一樣驗證。
/// </summary>
public sealed record AssistantToolCall(Guid Viewer, AssistantArguments Arguments, JsonElement? View)
{
    /// <summary>
    /// 參數都沒填時，把畫面條件讀成該頁查詢端點的參數型別（傳入與端點相同的 <c>JsonOptions</c>，欄位才同名同格式）。
    /// 沒有畫面條件時 <c>Missing</c>；型別不對時 <c>Invalid</c>，與查詢端點收到錯誤參數一樣當成條件不合法，其餘交給查詢本身驗證。
    /// </summary>
    public (T? View, AssistantViewState State) ReadView<T>(JsonSerializerOptions options) where T : class
    {
        if (!Arguments.IsEmpty || View is not { } view) return (null, AssistantViewState.Missing);
        try
        {
            return view.Deserialize<T>(options) is { } value ? (value, AssistantViewState.Read) : (null, AssistantViewState.Invalid);
        }
        catch (JsonException)
        {
            return (null, AssistantViewState.Invalid);
        }
    }
}

/// <summary><see cref="AssistantToolCall.ReadView{T}"/> 的結果。</summary>
public enum AssistantViewState
{
    Missing,
    Read,
    Invalid,
}

/// <summary>工具的結果：<paramref name="Scope"/> 是一行範圍說明（畫面顯示），<paramref name="Facts"/> 依重要性排序，放不下時從後面捨去。</summary>
public sealed record AssistantToolResult(string Scope, IReadOnlyList<AssistantFact> Facts);

public static class AssistantToolRegistration
{
    /// <summary>註冊助理工具。工具為 Scoped，與提問同一個請求範圍（同一個資料庫連線與稽核）。</summary>
    public static IServiceCollection AddAssistantTool<TTool>(this IServiceCollection services) where TTool : class, IAssistantTool
        => services.AddScoped<IAssistantTool, TTool>();
}
