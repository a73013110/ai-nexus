using Microsoft.EntityFrameworkCore;

namespace AiNexus.Platform.Diagnostics;

[Comment("共用診斷日誌；只保存受控且已遮罩的事件欄位，LogId 唯一用於補送去重。")]
public sealed class DiagnosticEvent
{
    [Comment("不可重複的日誌識別，SQL 補送去重鍵。")]
    public Guid LogId { get; set; } = Guid.NewGuid();
    [Comment("診斷事件發生的 UTC 時間，時間與 LogId 為排序及游標分頁鍵。")]
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
    [Comment("Microsoft.Extensions.Logging 層級值：Trace=0 到 Critical=5。")]
    public LogLevel Level { get; set; }
    [Comment("診斷事件的受控 Category 欄位；由集中日誌政策限制大小與遮罩。")]
    public string Category { get; set; } = "";
    [Comment("穩定的事件分類識別碼，跨程式版本保持意義一致。")]
    public int EventId { get; set; }
    [Comment("穩定的事件名稱，供模組及流程查詢。")]
    public string EventName { get; set; } = "";
    [Comment("結構化訊息模板，禁止串接內容與秘密。")]
    public string MessageTemplate { get; set; } = "";
    [Comment("白名單純量 metadata，大小及欄位數受限。")]
    public string PropertiesJson { get; set; } = "{}";
    [Comment("診斷事件的受控 Service 欄位；由集中日誌政策限制大小與遮罩。")]
    public string Service { get; set; } = "AiNexus";
    [Comment("診斷事件的受控 Environment 欄位；由集中日誌政策限制大小與遮罩。")]
    public string Environment { get; set; } = "";
    [Comment("應用程式 informational version，用於辨認發版。")]
    public string Version { get; set; } = "";
    [Comment("診斷事件的受控 Instance 欄位；由集中日誌政策限制大小與遮罩。")]
    public string Instance { get; set; } = "";
    [Comment("伺服器產生的不透明問題查證代碼；每個問題個別識別。")]
    public string? IssueCode { get; set; }
    [Comment("W3C 流程追蹤識別，僅由伺服器建立。")]
    public string? TraceId { get; set; }
    [Comment("診斷事件的受控 SpanId 欄位；由集中日誌政策限制大小與遮罩。")]
    public string? SpanId { get; set; }
    [Comment("診斷事件的受控 RequestId 欄位；由集中日誌政策限制大小與遮罩。")]
    public string? RequestId { get; set; }
    [Comment("持久作業識別，跨佇列與重試保持不變。")]
    public Guid? OperationId { get; set; }
    [Comment("關聯背景工作識別碼。")]
    public Guid? JobId { get; set; }
    [Comment("關聯生成或評測執行的識別碼。")]
    public Guid? RunId { get; set; }
    [Comment("伺服器解析的受控使用者識別碼；不接受客戶端傳入。")]
    public Guid? UserId { get; set; }
    [Comment("背景工作執行／重試次數。")]
    public int? Attempt { get; set; }
    [Comment("診斷事件的受控 Method 欄位；由集中日誌政策限制大小與遮罩。")]
    public string? Method { get; set; }
    [Comment("HTTP 路由模板，不含實際路徑值或查詢參數。")]
    public string? Route { get; set; }
    [Comment("診斷事件的受控 StatusCode 欄位；由集中日誌政策限制大小與遮罩。")]
    public int? StatusCode { get; set; }
    [Comment("診斷事件的受控 DurationMs 欄位；由集中日誌政策限制大小與遮罩。")]
    public double? DurationMs { get; set; }
    [Comment("診斷事件的受控 ExternalService 欄位；由集中日誌政策限制大小與遮罩。")]
    public string? ExternalService { get; set; }
    [Comment("對外安全的錯誤代碼，不含密碼或完整例外。")]
    public string? ErrorCode { get; set; }
    [Comment("診斷事件的受控 ExceptionType 欄位；由集中日誌政策限制大小與遮罩。")]
    public string? ExceptionType { get; set; }
    [Comment("省略例外自由文字與路徑的型別、錯誤碼及堆疊。")]
    public string? ExceptionDetail { get; set; }
    [Comment("明確標示不可信用戶端回報。")]
    public bool UntrustedClient { get; set; }
}
