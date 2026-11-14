-- 各申請種類的預設簽核流程（ADR 0036）。可重複執行：只新增還沒有流程的種類，不覆寫管理員之後的調整。
-- 種類由程式註冊（IFlowKind）；每個註冊的種類都要在這裡有預設流程（FlowSeedTests 檢查），否則送出申請會失敗。
-- 發版時在結構之後執行；整合測試的範本資料庫也執行這一份。
SET XACT_ABORT ON;
SET NOCOUNT ON;
BEGIN TRANSACTION;

DECLARE @now DATETIMEOFFSET (7) = SYSDATETIMEOFFSET();
DECLARE @steps TABLE ([Kind] NVARCHAR (64), [StepNumber] INT, [Name] NVARCHAR (60), [ApproverRule] NVARCHAR (64), [ApproverArgument] NVARCHAR (2000));
INSERT @steps VALUES
    -- 儲存庫權限：先由申請人的主管確認工作需要，再由儲存庫負責人確認內容可以給（ADR 0037）。
    (N'repository-access', 1, N'主管', N'applicant-manager', N'{"levels":1}'),
    (N'repository-access', 2, N'儲存庫負責人', N'repository-stewards', NULL);

INSERT [flow].[FlowDefinitions] ([Kind], [UpdatedAt])
SELECT DISTINCT s.[Kind], @now FROM @steps AS s
WHERE NOT EXISTS (SELECT 1 FROM [flow].[FlowDefinitions] AS d WHERE d.[Kind] = s.[Kind]);

-- 只有剛建立的流程才寫入預設關卡；既有的流程由管理員在「簽核流程」維護，不覆寫。
INSERT [flow].[FlowDefinitionSteps] ([Kind], [StepNumber], [Name], [ApproverRule], [ApproverArgument])
SELECT s.[Kind], s.[StepNumber], s.[Name], s.[ApproverRule], s.[ApproverArgument] FROM @steps AS s
JOIN [flow].[FlowDefinitions] AS d ON d.[Kind] = s.[Kind] AND d.[UpdatedAt] = @now;

COMMIT TRANSACTION;
