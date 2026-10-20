CREATE TABLE [mail].[DeliveryAttempts] (
    [MessageId]      UNIQUEIDENTIFIER   NOT NULL,
    [Attempt]        INT                NOT NULL,
    [Transport]      NVARCHAR (16)      NOT NULL,
    [StartedAt]      DATETIMEOFFSET (7) NOT NULL,
    [FinishedAt]     DATETIMEOFFSET (7) NOT NULL,
    [Outcome]        NVARCHAR (16)      NOT NULL,
    [SmtpStatus]     INT                NULL,
    [EnhancedStatus] NVARCHAR (16)      NULL,
    [Response]       NVARCHAR (512)     NULL,
    [ErrorCode]      NVARCHAR (80)      NULL,
    [IssueCode]      NVARCHAR (40)      NULL,
    [Stage]          NVARCHAR (16)      NULL,
    CONSTRAINT [pk_DeliveryAttempts] PRIMARY KEY CLUSTERED ([MessageId] ASC, [Attempt] ASC),
    CONSTRAINT [ck_DeliveryAttempts_Outcome] CHECK ([Outcome]=N'abandoned' OR [Outcome]=N'transport' OR [Outcome]=N'permanent' OR [Outcome]=N'transient' OR [Outcome]=N'sent'),
    CONSTRAINT [ck_DeliveryAttempts_Stage] CHECK ([Stage]=N'connect' OR [Stage]=N'tls' OR [Stage]=N'auth' OR [Stage]=N'sender' OR [Stage]=N'recipient' OR [Stage]=N'data'),
    CONSTRAINT [fk_DeliveryAttempts_OutboundMessages] FOREIGN KEY ([MessageId]) REFERENCES [mail].[OutboundMessages] ([Id]) ON DELETE CASCADE
);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'每一次寄送嘗試的結果，供查證「有沒有寄出、為什麼失敗」；刪除郵件時一併刪除。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'DeliveryAttempts';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'第幾次嘗試，對應認領當時的 OutboundMessages.Attempts。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'DeliveryAttempts', @level2type = N'COLUMN', @level2name = N'Attempt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'SMTP 擴充狀態碼（例如 5.1.1）；沒有時為 null。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'DeliveryAttempts', @level2type = N'COLUMN', @level2name = N'EnhancedStatus';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'失敗的分類代碼；成功為 null。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'DeliveryAttempts', @level2type = N'COLUMN', @level2name = N'ErrorCode';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'這次嘗試結束的時間（UTC）。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'DeliveryAttempts', @level2type = N'COLUMN', @level2name = N'FinishedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'非預期失敗的查證代碼（NX-…）。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'DeliveryAttempts', @level2type = N'COLUMN', @level2name = N'IssueCode';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'關聯郵件的 OutboundMessages 主鍵。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'DeliveryAttempts', @level2type = N'COLUMN', @level2name = N'MessageId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'結果：sent、transient（稍後重試）、permanent（不再重試）、transport（傳輸層故障，暫停寄送）、abandoned（租約過期未完成）。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'DeliveryAttempts', @level2type = N'COLUMN', @level2name = N'Outcome';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'成功時為 SMTP 最終回應；失敗時為伺服器回應或例外訊息（含 TLS 憑證摘要）。寫入前遮罩 email、網址與路徑並截斷 512 字元。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'DeliveryAttempts', @level2type = N'COLUMN', @level2name = N'Response';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'SMTP 回應碼（例如 250、451、550）；連線類失敗為 null。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'DeliveryAttempts', @level2type = N'COLUMN', @level2name = N'SmtpStatus';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'失敗發生在哪一步：connect（連線）、tls、auth（認證）、sender（MAIL FROM）、recipient（RCPT TO）、data（內容）；成功、本地拒絕與中止為 null。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'DeliveryAttempts', @level2type = N'COLUMN', @level2name = N'Stage';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'這次嘗試開始的時間（UTC）。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'DeliveryAttempts', @level2type = N'COLUMN', @level2name = N'StartedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'使用的傳輸（Smtp、Pickup）。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'DeliveryAttempts', @level2type = N'COLUMN', @level2name = N'Transport';


GO

