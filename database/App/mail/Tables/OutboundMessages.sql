CREATE TABLE [mail].[OutboundMessages] (
    [Id]               UNIQUEIDENTIFIER   NOT NULL,
    [Category]         NVARCHAR (64)      NOT NULL,
    [Priority]         TINYINT            NOT NULL,
    [Status]           NVARCHAR (16)      NOT NULL,
    [Sensitive]        BIT                NOT NULL,
    [IdempotencyKey]   NVARCHAR (200)     NULL,
    [BatchId]          UNIQUEIDENTIFIER   NULL,
    [ResendOfId]       UNIQUEIDENTIFIER   NULL,
    [RequestedById]    UNIQUEIDENTIFIER   NULL,
    [RecipientUserId]  UNIQUEIDENTIFIER   NULL,
    [RecipientAddress] NVARCHAR (254)     NULL,
    [RecipientName]    NVARCHAR (120)     NULL,
    [RecipientMasked]  NVARCHAR (254)     NOT NULL,
    [RecipientHash]    BINARY (32)        NOT NULL,
    [FromAddress]      NVARCHAR (254)     NOT NULL,
    [Subject]          NVARCHAR (200)     NOT NULL,
    [HeaderMessageId]  NVARCHAR (255)     NOT NULL,
    [Transport]        NVARCHAR (16)      NULL,
    [Attempts]         INT                NOT NULL,
    [NextAttemptAt]    DATETIMEOFFSET (7) NOT NULL,
    [ExpiresAt]        DATETIMEOFFSET (7) NULL,
    [LeaseToken]       UNIQUEIDENTIFIER   NULL,
    [LeaseUntil]       DATETIMEOFFSET (7) NULL,
    [ProviderResponse] NVARCHAR (512)     NULL,
    [ErrorCode]        NVARCHAR (80)      NULL,
    [IssueCode]        NVARCHAR (40)      NULL,
    [TraceId]          NVARCHAR (32)      NULL,
    [CreatedAt]        DATETIMEOFFSET (7) NOT NULL,
    [UpdatedAt]        DATETIMEOFFSET (7) NOT NULL,
    [SentAt]           DATETIMEOFFSET (7) NULL,
    [ContentPurgedAt]  DATETIMEOFFSET (7) NULL,
    CONSTRAINT [pk_OutboundMessages] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [ck_OutboundMessages_Attempts] CHECK ([Attempts]>=(0)),
    CONSTRAINT [ck_OutboundMessages_Priority] CHECK ([Priority]<=(2)),
    CONSTRAINT [ck_OutboundMessages_Status] CHECK ([Status]=N'cancelled' OR [Status]=N'failed' OR [Status]=N'sent' OR [Status]=N'sending' OR [Status]=N'queued'),
    CONSTRAINT [fk_OutboundMessages_RecipientUserId] FOREIGN KEY ([RecipientUserId]) REFERENCES [identity].[Users] ([Id]),
    CONSTRAINT [fk_OutboundMessages_RequestedById] FOREIGN KEY ([RequestedById]) REFERENCES [identity].[Users] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [ix_OutboundMessages_BatchId]
    ON [mail].[OutboundMessages]([BatchId] ASC) WHERE ([BatchId] IS NOT NULL);


GO

CREATE NONCLUSTERED INDEX [ix_OutboundMessages_CreatedAt]
    ON [mail].[OutboundMessages]([CreatedAt] DESC, [Id] DESC)
    INCLUDE([Status], [Category]);


GO

CREATE NONCLUSTERED INDEX [ix_OutboundMessages_Pending]
    ON [mail].[OutboundMessages]([Priority] DESC, [NextAttemptAt] ASC)
    INCLUDE([Status], [LeaseUntil]) WHERE ([Status] IN (N'queued', N'sending'));


GO

CREATE NONCLUSTERED INDEX [ix_OutboundMessages_RecipientHash]
    ON [mail].[OutboundMessages]([RecipientHash] ASC, [CreatedAt] DESC);


GO

CREATE NONCLUSTERED INDEX [ix_OutboundMessages_RecipientUserId]
    ON [mail].[OutboundMessages]([RecipientUserId] ASC) WHERE ([RecipientUserId] IS NOT NULL);


GO

CREATE NONCLUSTERED INDEX [ix_OutboundMessages_RequestedById]
    ON [mail].[OutboundMessages]([RequestedById] ASC) WHERE ([RequestedById] IS NOT NULL);


GO

CREATE NONCLUSTERED INDEX [ix_OutboundMessages_SentAt]
    ON [mail].[OutboundMessages]([SentAt] ASC) WHERE ([SentAt] IS NOT NULL);


GO

CREATE UNIQUE NONCLUSTERED INDEX [ux_OutboundMessages_IdempotencyKey]
    ON [mail].[OutboundMessages]([IdempotencyKey] ASC) WHERE ([IdempotencyKey] IS NOT NULL);


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'待寄與已寄的郵件（transactional outbox）：一封信一位收件者一列，與業務變更同一交易入列，由 MailDispatcher 認領寄出。「已寄出」只代表 SMTP relay 已接受，不代表已投遞。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'OutboundMessages';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'已認領寄送的次數；認領時加一。放棄以 GiveUpAfterHours 的時間為準，不以次數為準。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'OutboundMessages', @level2type = N'COLUMN', @level2name = N'Attempts';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'同一次批次入列的識別碼；單封信為 null。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'OutboundMessages', @level2type = N'COLUMN', @level2name = N'BatchId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'信件分類（範本宣告，例如 account.email-change-code），供篩選與統計。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'OutboundMessages', @level2type = N'COLUMN', @level2name = N'Category';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'內文已刪除的時間：敏感信寄出或結束後立即刪除，其餘依 Mail:Retention:ContentDays 刪除；null 表示內文仍在。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'OutboundMessages', @level2type = N'COLUMN', @level2name = N'ContentPurgedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'入列時間（UTC）；管理列表遞減游標與保存期限的依據。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'OutboundMessages', @level2type = N'COLUMN', @level2name = N'CreatedAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'最近一次失敗的分類代碼（mail_rejected、mail_expired、mail_gave_up、mail_transport_unavailable 等）；成功寄出時清空。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'OutboundMessages', @level2type = N'COLUMN', @level2name = N'ErrorCode';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'過了這個時間就不再寄出（驗證碼等有時效的信）；null 表示不過期。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'OutboundMessages', @level2type = N'COLUMN', @level2name = N'ExpiresAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'入列當時設定的寄件地址。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'OutboundMessages', @level2type = N'COLUMN', @level2name = N'FromAddress';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'我方產生的 Message-ID 標頭（<Id@網域>），用來在 mail server 或 Google 的紀錄比對；重寄同一列時不變，讓收件端有機會去重。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'OutboundMessages', @level2type = N'COLUMN', @level2name = N'HeaderMessageId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'資料的主鍵識別碼，由伺服器產生；也是 Message-ID 的本體。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'OutboundMessages', @level2type = N'COLUMN', @level2name = N'Id';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'冪等鍵（<category>:<業務 ID>[:<收件者>]）；已存在時不重複入列。null 表示不檢查。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'OutboundMessages', @level2type = N'COLUMN', @level2name = N'IdempotencyKey';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'最近一次非預期失敗的查證代碼（NX-…），可在系統日誌查到對應事件。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'OutboundMessages', @level2type = N'COLUMN', @level2name = N'IssueCode';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'寄送中的租約 token；寫回結果時比對（fencing），過期的 worker 不能覆寫。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'OutboundMessages', @level2type = N'COLUMN', @level2name = N'LeaseToken';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'租約到期時間；過期仍是 sending 的列會被重新認領，並補一筆 abandoned 嘗試。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'OutboundMessages', @level2type = N'COLUMN', @level2name = N'LeaseUntil';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'最早可再認領的時間；入列時為當下，暫時性失敗後依退避時間延後。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'OutboundMessages', @level2type = N'COLUMN', @level2name = N'NextAttemptAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'認領優先序：2＝安全（驗證碼、密碼變更通知）、1＝一般、0＝大量；數字大的先寄。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'OutboundMessages', @level2type = N'COLUMN', @level2name = N'Priority';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'傳輸的最終回應（SMTP 2xx 回應或 pickup:<路徑>），寫入前遮罩 email 並截斷 512 字元。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'OutboundMessages', @level2type = N'COLUMN', @level2name = N'ProviderResponse';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'收件地址；超過 Mail:Retention:RecipientDays 後清除（之後不能重寄）。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'OutboundMessages', @level2type = N'COLUMN', @level2name = N'RecipientAddress';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'收件地址（小寫正規化）的 SHA-256，供以地址搜尋；是查詢鍵，不是匿名化，公司地址可被猜出。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'OutboundMessages', @level2type = N'COLUMN', @level2name = N'RecipientHash';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'遮罩後的收件地址（c***n@company.com），列表與詳情顯示用；地址清除後仍保留。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'OutboundMessages', @level2type = N'COLUMN', @level2name = N'RecipientMasked';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'收件者顯示名稱；與地址同時清除。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'OutboundMessages', @level2type = N'COLUMN', @level2name = N'RecipientName';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'收件者是平台使用者時的 Users 主鍵；外部地址為 null。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'OutboundMessages', @level2type = N'COLUMN', @level2name = N'RecipientUserId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'觸發寄信的使用者（自助操作者、重寄或測試信的管理員）；系統自動寄出為 null。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'OutboundMessages', @level2type = N'COLUMN', @level2name = N'RequestedById';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'重寄來源信的 Id；刻意不設外鍵，保存清理刪除原信時不受重寄信阻擋。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'OutboundMessages', @level2type = N'COLUMN', @level2name = N'ResendOfId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'是否為敏感信（驗證碼等）：內文加密保存、寄出或結束後立即刪除，管理端看不到內文也不能重寄。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'OutboundMessages', @level2type = N'COLUMN', @level2name = N'Sensitive';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'傳輸接受的時間；也用於 Mail:MaxPerDay 的 24 小時計數。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'OutboundMessages', @level2type = N'COLUMN', @level2name = N'SentAt';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'狀態：queued、sending、sent、failed、cancelled；等待重試是 queued 且 NextAttemptAt 在未來。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'OutboundMessages', @level2type = N'COLUMN', @level2name = N'Status';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'主旨（200 字元以內）；範本不得把驗證碼等秘密放進主旨。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'OutboundMessages', @level2type = N'COLUMN', @level2name = N'Subject';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'入列請求的 W3C TraceId，供查證時對照系統日誌。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'OutboundMessages', @level2type = N'COLUMN', @level2name = N'TraceId';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'最近一次寄送使用的傳輸（Smtp、Pickup）。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'OutboundMessages', @level2type = N'COLUMN', @level2name = N'Transport';


GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'最近一次狀態變更的時間（UTC）。', @level0type = N'SCHEMA', @level0name = N'mail', @level1type = N'TABLE', @level1name = N'OutboundMessages', @level2type = N'COLUMN', @level2name = N'UpdatedAt';


GO

