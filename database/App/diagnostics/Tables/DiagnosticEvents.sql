CREATE TABLE [diagnostics].[DiagnosticEvents] (
    [LogId]           UNIQUEIDENTIFIER   NOT NULL,
    [At]              DATETIMEOFFSET (7) NOT NULL,
    [Level]           INT                NOT NULL,
    [Category]        NVARCHAR (180)     NOT NULL,
    [EventId]         INT                NOT NULL,
    [EventName]       NVARCHAR (100)     NOT NULL,
    [MessageTemplate] NVARCHAR (2048)    NOT NULL,
    [PropertiesJson]  NVARCHAR (MAX)     NOT NULL,
    [Service]         NVARCHAR (80)      NOT NULL,
    [Environment]     NVARCHAR (32)      NOT NULL,
    [Version]         NVARCHAR (80)      NOT NULL,
    [Instance]        NVARCHAR (100)     NOT NULL,
    [IssueCode]       NVARCHAR (40)      NULL,
    [TraceId]         NVARCHAR (32)      NULL,
    [SpanId]          NVARCHAR (16)      NULL,
    [RequestId]       NVARCHAR (40)      NULL,
    [OperationId]     UNIQUEIDENTIFIER   NULL,
    [JobId]           UNIQUEIDENTIFIER   NULL,
    [RunId]           UNIQUEIDENTIFIER   NULL,
    [UserId]          UNIQUEIDENTIFIER   NULL,
    [Attempt]         INT                NULL,
    [Method]          NVARCHAR (10)      NULL,
    [Route]           NVARCHAR (240)     NULL,
    [StatusCode]      INT                NULL,
    [DurationMs]      FLOAT (53)         NULL,
    [ExternalService] NVARCHAR (32)      NULL,
    [ErrorCode]       NVARCHAR (80)      NULL,
    [ExceptionType]   NVARCHAR (180)     NULL,
    [ExceptionDetail] NVARCHAR (MAX)     NULL,
    [UntrustedClient] BIT                NOT NULL,
    CONSTRAINT [PK_DiagnosticEvents] PRIMARY KEY NONCLUSTERED ([LogId] ASC)
);


GO

CREATE CLUSTERED INDEX [IX_DiagnosticEvents_At_LogId]
    ON [diagnostics].[DiagnosticEvents]([At] DESC, [LogId] DESC);


GO

CREATE NONCLUSTERED INDEX [IX_DiagnosticEvents_Category_At_LogId]
    ON [diagnostics].[DiagnosticEvents]([Category] ASC, [At] ASC, [LogId] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_DiagnosticEvents_ErrorCode_At_LogId]
    ON [diagnostics].[DiagnosticEvents]([ErrorCode] ASC, [At] ASC, [LogId] ASC) WHERE ([ErrorCode] IS NOT NULL);


GO

CREATE NONCLUSTERED INDEX [IX_DiagnosticEvents_EventId_At_LogId]
    ON [diagnostics].[DiagnosticEvents]([EventId] ASC, [At] ASC, [LogId] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_DiagnosticEvents_EventName_At_LogId]
    ON [diagnostics].[DiagnosticEvents]([EventName] ASC, [At] ASC, [LogId] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_DiagnosticEvents_Instance_At_LogId]
    ON [diagnostics].[DiagnosticEvents]([Instance] ASC, [At] ASC, [LogId] ASC) WHERE ([Instance] IS NOT NULL);


GO

CREATE NONCLUSTERED INDEX [IX_DiagnosticEvents_IssueCode_At_LogId]
    ON [diagnostics].[DiagnosticEvents]([IssueCode] ASC, [At] ASC, [LogId] ASC) WHERE ([IssueCode] IS NOT NULL);


GO

CREATE NONCLUSTERED INDEX [IX_DiagnosticEvents_JobId_At_LogId]
    ON [diagnostics].[DiagnosticEvents]([JobId] ASC, [At] ASC, [LogId] ASC) WHERE ([JobId] IS NOT NULL);


GO

CREATE NONCLUSTERED INDEX [IX_DiagnosticEvents_Level_At_LogId]
    ON [diagnostics].[DiagnosticEvents]([Level] ASC, [At] ASC, [LogId] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_DiagnosticEvents_OperationId_At_LogId]
    ON [diagnostics].[DiagnosticEvents]([OperationId] ASC, [At] ASC, [LogId] ASC) WHERE ([OperationId] IS NOT NULL);


GO

CREATE NONCLUSTERED INDEX [IX_DiagnosticEvents_RunId_At_LogId]
    ON [diagnostics].[DiagnosticEvents]([RunId] ASC, [At] ASC, [LogId] ASC) WHERE ([RunId] IS NOT NULL);


GO

CREATE NONCLUSTERED INDEX [IX_DiagnosticEvents_TraceId_At_LogId]
    ON [diagnostics].[DiagnosticEvents]([TraceId] ASC, [At] ASC, [LogId] ASC) WHERE ([TraceId] IS NOT NULL);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'共用診斷日誌；只保存受控且已遮罩的事件欄位，LogId 唯一用於補送去重。', @level0type = N'SCHEMA', @level0name = N'diagnostics', @level1type = N'TABLE', @level1name = N'DiagnosticEvents';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'診斷事件發生的 UTC 時間，時間與 LogId 為排序及游標分頁鍵。', @level0type = N'SCHEMA', @level0name = N'diagnostics', @level1type = N'TABLE', @level1name = N'DiagnosticEvents', @level2type = N'COLUMN', @level2name = N'At';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'背景工作執行／重試次數。', @level0type = N'SCHEMA', @level0name = N'diagnostics', @level1type = N'TABLE', @level1name = N'DiagnosticEvents', @level2type = N'COLUMN', @level2name = N'Attempt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'診斷事件的受控 Category 欄位；由集中日誌政策限制大小與遮罩。', @level0type = N'SCHEMA', @level0name = N'diagnostics', @level1type = N'TABLE', @level1name = N'DiagnosticEvents', @level2type = N'COLUMN', @level2name = N'Category';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'診斷事件的受控 DurationMs 欄位；由集中日誌政策限制大小與遮罩。', @level0type = N'SCHEMA', @level0name = N'diagnostics', @level1type = N'TABLE', @level1name = N'DiagnosticEvents', @level2type = N'COLUMN', @level2name = N'DurationMs';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'診斷事件的受控 Environment 欄位；由集中日誌政策限制大小與遮罩。', @level0type = N'SCHEMA', @level0name = N'diagnostics', @level1type = N'TABLE', @level1name = N'DiagnosticEvents', @level2type = N'COLUMN', @level2name = N'Environment';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'對外安全的錯誤代碼，不含密碼或完整例外。', @level0type = N'SCHEMA', @level0name = N'diagnostics', @level1type = N'TABLE', @level1name = N'DiagnosticEvents', @level2type = N'COLUMN', @level2name = N'ErrorCode';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'穩定的事件分類識別碼，跨程式版本保持意義一致。', @level0type = N'SCHEMA', @level0name = N'diagnostics', @level1type = N'TABLE', @level1name = N'DiagnosticEvents', @level2type = N'COLUMN', @level2name = N'EventId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'穩定的事件名稱，供模組及流程查詢。', @level0type = N'SCHEMA', @level0name = N'diagnostics', @level1type = N'TABLE', @level1name = N'DiagnosticEvents', @level2type = N'COLUMN', @level2name = N'EventName';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'省略例外自由文字與路徑的型別、錯誤碼及堆疊。', @level0type = N'SCHEMA', @level0name = N'diagnostics', @level1type = N'TABLE', @level1name = N'DiagnosticEvents', @level2type = N'COLUMN', @level2name = N'ExceptionDetail';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'診斷事件的受控 ExceptionType 欄位；由集中日誌政策限制大小與遮罩。', @level0type = N'SCHEMA', @level0name = N'diagnostics', @level1type = N'TABLE', @level1name = N'DiagnosticEvents', @level2type = N'COLUMN', @level2name = N'ExceptionType';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'診斷事件的受控 ExternalService 欄位；由集中日誌政策限制大小與遮罩。', @level0type = N'SCHEMA', @level0name = N'diagnostics', @level1type = N'TABLE', @level1name = N'DiagnosticEvents', @level2type = N'COLUMN', @level2name = N'ExternalService';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'診斷事件的受控 Instance 欄位；由集中日誌政策限制大小與遮罩。', @level0type = N'SCHEMA', @level0name = N'diagnostics', @level1type = N'TABLE', @level1name = N'DiagnosticEvents', @level2type = N'COLUMN', @level2name = N'Instance';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'伺服器產生的不透明問題查證代碼；每個問題個別識別。', @level0type = N'SCHEMA', @level0name = N'diagnostics', @level1type = N'TABLE', @level1name = N'DiagnosticEvents', @level2type = N'COLUMN', @level2name = N'IssueCode';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯背景工作識別碼。', @level0type = N'SCHEMA', @level0name = N'diagnostics', @level1type = N'TABLE', @level1name = N'DiagnosticEvents', @level2type = N'COLUMN', @level2name = N'JobId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Microsoft.Extensions.Logging 層級值：Trace=0 到 Critical=5。', @level0type = N'SCHEMA', @level0name = N'diagnostics', @level1type = N'TABLE', @level1name = N'DiagnosticEvents', @level2type = N'COLUMN', @level2name = N'Level';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'不可重複的日誌識別，SQL 補送去重鍵。', @level0type = N'SCHEMA', @level0name = N'diagnostics', @level1type = N'TABLE', @level1name = N'DiagnosticEvents', @level2type = N'COLUMN', @level2name = N'LogId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'結構化訊息模板，禁止串接內容與秘密。', @level0type = N'SCHEMA', @level0name = N'diagnostics', @level1type = N'TABLE', @level1name = N'DiagnosticEvents', @level2type = N'COLUMN', @level2name = N'MessageTemplate';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'診斷事件的受控 Method 欄位；由集中日誌政策限制大小與遮罩。', @level0type = N'SCHEMA', @level0name = N'diagnostics', @level1type = N'TABLE', @level1name = N'DiagnosticEvents', @level2type = N'COLUMN', @level2name = N'Method';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'持久作業識別，跨佇列與重試保持不變。', @level0type = N'SCHEMA', @level0name = N'diagnostics', @level1type = N'TABLE', @level1name = N'DiagnosticEvents', @level2type = N'COLUMN', @level2name = N'OperationId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'白名單純量 metadata，大小及欄位數受限。', @level0type = N'SCHEMA', @level0name = N'diagnostics', @level1type = N'TABLE', @level1name = N'DiagnosticEvents', @level2type = N'COLUMN', @level2name = N'PropertiesJson';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'診斷事件的受控 RequestId 欄位；由集中日誌政策限制大小與遮罩。', @level0type = N'SCHEMA', @level0name = N'diagnostics', @level1type = N'TABLE', @level1name = N'DiagnosticEvents', @level2type = N'COLUMN', @level2name = N'RequestId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'HTTP 路由模板，不含實際路徑值或查詢參數。', @level0type = N'SCHEMA', @level0name = N'diagnostics', @level1type = N'TABLE', @level1name = N'DiagnosticEvents', @level2type = N'COLUMN', @level2name = N'Route';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯生成或評測執行的識別碼。', @level0type = N'SCHEMA', @level0name = N'diagnostics', @level1type = N'TABLE', @level1name = N'DiagnosticEvents', @level2type = N'COLUMN', @level2name = N'RunId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'診斷事件的受控 Service 欄位；由集中日誌政策限制大小與遮罩。', @level0type = N'SCHEMA', @level0name = N'diagnostics', @level1type = N'TABLE', @level1name = N'DiagnosticEvents', @level2type = N'COLUMN', @level2name = N'Service';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'診斷事件的受控 SpanId 欄位；由集中日誌政策限制大小與遮罩。', @level0type = N'SCHEMA', @level0name = N'diagnostics', @level1type = N'TABLE', @level1name = N'DiagnosticEvents', @level2type = N'COLUMN', @level2name = N'SpanId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'診斷事件的受控 StatusCode 欄位；由集中日誌政策限制大小與遮罩。', @level0type = N'SCHEMA', @level0name = N'diagnostics', @level1type = N'TABLE', @level1name = N'DiagnosticEvents', @level2type = N'COLUMN', @level2name = N'StatusCode';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'W3C 流程追蹤識別，僅由伺服器建立。', @level0type = N'SCHEMA', @level0name = N'diagnostics', @level1type = N'TABLE', @level1name = N'DiagnosticEvents', @level2type = N'COLUMN', @level2name = N'TraceId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'明確標示不可信用戶端回報。', @level0type = N'SCHEMA', @level0name = N'diagnostics', @level1type = N'TABLE', @level1name = N'DiagnosticEvents', @level2type = N'COLUMN', @level2name = N'UntrustedClient';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'伺服器解析的受控使用者識別碼；不接受客戶端傳入。', @level0type = N'SCHEMA', @level0name = N'diagnostics', @level1type = N'TABLE', @level1name = N'DiagnosticEvents', @level2type = N'COLUMN', @level2name = N'UserId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'應用程式 informational version，用於辨認發版。', @level0type = N'SCHEMA', @level0name = N'diagnostics', @level1type = N'TABLE', @level1name = N'DiagnosticEvents', @level2type = N'COLUMN', @level2name = N'Version';


GO

