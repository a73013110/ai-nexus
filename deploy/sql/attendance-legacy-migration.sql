/*
================================================================================
出勤模組舊資料轉移：ADAuthentication 的員工卡號與刷卡 → 本系統 attendance schema
================================================================================
用途
  把舊系統的刷卡（dbo.EmpClock）與卡號對應（dbo.EmpCard、EmpCardLog）轉成
  attendance.Cards、CardAssignments、Punches（加一組代表「舊系統轉入」的 PunchFiles／PunchImports）。
  規則見 ADR 0018「舊資料轉移」與 docs/operations/attendance-migration.md。

前置條件
  - 在舊庫所在的主機執行（ADAuthentication 與 GD_HL_EF 同機），SSMS 開「查詢 → SQLCMD 模式」。
  - 該主機有指向本系統資料庫主機的 linked server（名稱＝TargetServer），可讀寫 attendance 與讀 identity.Users。
  - 來源庫的 dbo.syn_eDoc_Sys_User synonym 指向 GD 的 Sys_User（舊系統原本就用它）。
  - 本系統資料庫已部署 attendance 的資料表。

變數（下方 :setvar）
  Apply                 0＝只預覽（不寫入）；1＝寫入。
  SourceDatabase        舊庫名稱。
  TargetServer          linked server 名稱。
  TargetDatabase        本系統資料庫名稱。
  BatchSize             刷卡每批寫入筆數。
  LegacyFileName        代表舊系統轉入的 PunchFiles.FileName；刻意不是 yyyyMMdd.txt，排程與手動重匯都不會碰它。
  LegacyMachineCode     舊表沒有存機器代號與事件代號，以打卡檔的值補上；機器代號是去重鍵的一部分，
  LegacyEventCode       必須與打卡檔第一欄相同，日後匯入同一天的檔案才會認出是同一筆刷卡。
  UnmatchedAsHolderName 0＝對不上使用者的持有期間不寫（列清單，預設）；1＝改以姓名寫入（UserId 為空，HR 之後綁人）。

執行步驟
  1. Apply 0 執行，看各結果集：將新增筆數、對不上的人、無法寫入的持有期間、無法轉入的刷卡。
  2. 在 Identity 補使用者的 AD 帳號、或在舊庫修正 EmpCard，再預覽到清單可接受為止。
  3. 舊系統停止匯入後 Apply 1 執行。中斷或失敗時直接重跑：已存在的列不重複寫、也不覆寫。
  4. 再跑一次 Apply 0，各表將新增應為 0。

時間
  舊表是沒有時區的台灣時間，一律以 +08:00 解讀（與匯入程式的 TaipeiTime 相同；台灣沒有日光節約）。
  CreatedAt 與匯入紀錄的時間用 UTC，與程式的 TimeProvider 一致。
================================================================================
*/
:setvar Apply 0
:setvar SourceDatabase "ADAuthentication"
:setvar TargetServer "192.168.2.95"
:setvar TargetDatabase "AiNexus"
:setvar BatchSize 50000
:setvar LegacyFileName "legacy-adauthentication"
:setvar LegacyMachineCode "001"
:setvar LegacyEventCode "11"
:setvar UnmatchedAsHolderName 0
:on error exit

SET NOCOUNT ON;
-- 寫入 linked server 的 OLE DB 來源要求 XACT_ABORT；每個 INSERT 是各自的 autocommit 陳述式，不開跨伺服器交易，不需要 MSDTC。
SET XACT_ABORT ON;
GO

-- ============================================================================
-- 0. 檢查變數
-- ============================================================================
IF N'$(Apply)' NOT IN (N'0', N'1') THROW 50000, N'Apply 只能是 0 或 1。', 1;
IF N'$(UnmatchedAsHolderName)' NOT IN (N'0', N'1') THROW 50000, N'UnmatchedAsHolderName 只能是 0 或 1。', 1;
IF ISNULL(TRY_CONVERT(INT, N'$(BatchSize)'), 0) NOT BETWEEN 1000 AND 500000 THROW 50000, N'BatchSize 須介於 1000 與 500000。', 1;
-- 檔名若像每日檔，排程掃描或 HR 手動重匯會把它當成真的檔案處理，刪掉舊系統轉入的刷卡。
IF N'$(LegacyFileName)' LIKE N'[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9].txt' OR LEN(N'$(LegacyFileName)') NOT BETWEEN 1 AND 128
    THROW 50000, N'LegacyFileName 不可是 yyyyMMdd.txt 的形式，長度 1～128。', 1;
IF LEN(N'$(LegacyMachineCode)') NOT BETWEEN 1 AND 16 OR LEN(N'$(LegacyEventCode)') NOT BETWEEN 1 AND 16
    THROW 50000, N'LegacyMachineCode、LegacyEventCode 長度須為 1～16。', 1;

IF OBJECT_ID('tempdb..#RunInfo') IS NOT NULL DROP TABLE #RunInfo;
CREATE TABLE #RunInfo (StartedAt DATETIMEOFFSET(7) NOT NULL);
INSERT #RunInfo VALUES (SWITCHOFFSET(SYSDATETIMEOFFSET(), '+00:00'));
PRINT N'模式：' + CASE N'$(Apply)' WHEN N'1' THEN N'寫入（Apply 1）' ELSE N'預覽（Apply 0，不寫入）' END
    + N'；來源 [$(SourceDatabase)] → [$(TargetServer)].[$(TargetDatabase)]';
GO

-- ============================================================================
-- 1. 讀來源到本地暫存表
--    之後所有比對都在暫存表之間做，不逐列跨庫查詢，也避開來源庫、GD 與 tempdb 定序不同造成的衝突。
--    字串欄一律用目標資料庫的定序（Chinese_Taiwan_Stroke_CI_AS）：卡號不分大小寫、與 ux_Cards_CardNumber 一致。
-- ============================================================================
IF OBJECT_ID('tempdb..#Clock') IS NOT NULL DROP TABLE #Clock;
CREATE TABLE #Clock (
    CardRaw    VARCHAR(50)  COLLATE Chinese_Taiwan_Stroke_CI_AS NOT NULL,
    CardNumber NVARCHAR(50) COLLATE Chinese_Taiwan_Stroke_CI_AS NOT NULL,
    EmpNo      VARCHAR(50)  COLLATE Chinese_Taiwan_Stroke_CI_AS NOT NULL,
    ClockDTTM  DATETIME          NOT NULL,
    PunchedAt  DATETIMEOFFSET(7) NOT NULL,
    Problem    NVARCHAR(60) NULL
);
-- datetime 轉 datetime2(7) 不捨入：舊資料是整秒，維持原值才會與日後匯入同一天檔案的刷卡被認成同一筆。
INSERT #Clock (CardRaw, CardNumber, EmpNo, ClockDTTM, PunchedAt)
SELECT c.CardNo, LTRIM(RTRIM(CONVERT(NVARCHAR(50), c.CardNo))), LTRIM(RTRIM(c.EmpNo)), c.ClockDTTM,
       TODATETIMEOFFSET(CONVERT(DATETIME2(7), c.ClockDTTM), '+08:00')
FROM [$(SourceDatabase)].dbo.EmpClock AS c;

-- 卡號規則同匯入程式（PunchFileParser）：去掉前後空白、1～32 字、不含控制字元；日期 2000～2099 年。
-- 打卡機時鐘重設產生的怪日期不轉：它會成為持有期間的起點，讓期間往前延伸到不相干的年代。
DECLARE @control NVARCHAR(20) = N'%[' + NCHAR(1) + N'-' + NCHAR(31) + NCHAR(127) + N'-' + NCHAR(159) + N']%';
UPDATE #Clock SET Problem =
    CASE WHEN CardNumber = N'' THEN N'卡號空白'
         WHEN LEN(CardNumber) > 32 THEN N'卡號超過 32 字'
         WHEN PATINDEX(@control, CardNumber COLLATE Latin1_General_BIN2) > 0 THEN N'卡號含控制字元'
         WHEN YEAR(ClockDTTM) NOT BETWEEN 2000 AND 2099 THEN N'刷卡日期不在 2000～2099 年' END;

-- 舊表主鍵是（原始卡號、時間），去掉空白後可能撞在一起；同一筆刷卡只轉一次。
WITH d AS (SELECT Problem, ROW_NUMBER() OVER (PARTITION BY CardNumber, PunchedAt ORDER BY CardRaw) AS n FROM #Clock WHERE Problem IS NULL)
UPDATE d SET Problem = N'卡號去空白後與另一筆刷卡重複' WHERE n > 1;

CREATE CLUSTERED INDEX cx_Clock ON #Clock (CardNumber, PunchedAt);

IF OBJECT_ID('tempdb..#EmpCard') IS NOT NULL DROP TABLE #EmpCard;
SELECT CONVERT(VARCHAR(50), LTRIM(RTRIM(EmpNo))) COLLATE Chinese_Taiwan_Stroke_CI_AS AS EmpNo,
       CONVERT(NVARCHAR(50), EmpName) COLLATE Chinese_Taiwan_Stroke_CI_AS AS EmpName,
       CONVERT(NVARCHAR(50), LTRIM(RTRIM(Account))) COLLATE Chinese_Taiwan_Stroke_CI_AS AS GdId,
       CONVERT(NVARCHAR(50), LTRIM(RTRIM(CardNo))) COLLATE Chinese_Taiwan_Stroke_CI_AS AS CardNumber,
       TODATETIMEOFFSET(CONVERT(DATETIME2(7), UpdateDTTM), '+08:00') AS UpdatedAt
INTO #EmpCard
FROM [$(SourceDatabase)].dbo.EmpCard;

-- 刪除的對應（離職者）只留在 EmpCardLog；EmpCard 找不到職號時才用它換帳號。
IF OBJECT_ID('tempdb..#EmpCardLog') IS NOT NULL DROP TABLE #EmpCardLog;
SELECT CONVERT(VARCHAR(50), LTRIM(RTRIM(EmpNo))) COLLATE Chinese_Taiwan_Stroke_CI_AS AS EmpNo,
       CONVERT(NVARCHAR(50), EmpName) COLLATE Chinese_Taiwan_Stroke_CI_AS AS EmpName,
       CONVERT(NVARCHAR(50), LTRIM(RTRIM(Account))) COLLATE Chinese_Taiwan_Stroke_CI_AS AS GdId,
       UpdateDTTM
INTO #EmpCardLog
FROM [$(SourceDatabase)].dbo.EmpCardLog;

IF OBJECT_ID('tempdb..#SysUser') IS NOT NULL DROP TABLE #SysUser;
SELECT CONVERT(NVARCHAR(50), LTRIM(RTRIM(ID))) COLLATE Chinese_Taiwan_Stroke_CI_AS AS GdId,
       CONVERT(NVARCHAR(256), LTRIM(RTRIM(ISNULL(ACCOUNT, N'')))) COLLATE Chinese_Taiwan_Stroke_CI_AS AS AdAccount
INTO #SysUser
FROM [$(SourceDatabase)].dbo.syn_eDoc_Sys_User;
GO

-- ============================================================================
-- 2. 讀目標現況到本地暫存表（一次讀完，之後本地 join）
-- ============================================================================
IF OBJECT_ID('tempdb..#TargetUsers') IS NOT NULL DROP TABLE #TargetUsers;
CREATE TABLE #TargetUsers (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    AdAccount   NVARCHAR(64)  COLLATE Chinese_Taiwan_Stroke_CI_AS NOT NULL,
    DisplayName NVARCHAR(256) COLLATE Chinese_Taiwan_Stroke_CI_AS NOT NULL,
    IsDeleted   BIT NOT NULL
);
INSERT #TargetUsers
SELECT Id, AdAccount, DisplayName, CASE WHEN DeletedAt IS NULL THEN 0 ELSE 1 END
FROM [$(TargetServer)].[$(TargetDatabase)].[identity].[Users]
WHERE AdAccount IS NOT NULL;

IF OBJECT_ID('tempdb..#TargetCards') IS NOT NULL DROP TABLE #TargetCards;
CREATE TABLE #TargetCards (
    Id         UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    CardNumber NVARCHAR(32) COLLATE Chinese_Taiwan_Stroke_CI_AS NOT NULL UNIQUE,
    Kind       NVARCHAR(16) COLLATE Chinese_Taiwan_Stroke_CI_AS NOT NULL
);
INSERT #TargetCards SELECT Id, CardNumber, Kind FROM [$(TargetServer)].[$(TargetDatabase)].[attendance].[Cards];

IF OBJECT_ID('tempdb..#TargetAssignments') IS NOT NULL DROP TABLE #TargetAssignments;
CREATE TABLE #TargetAssignments (
    Id       UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    CardId   UNIQUEIDENTIFIER NOT NULL,
    UserId   UNIQUEIDENTIFIER NULL,
    StartsAt DATETIMEOFFSET(7) NOT NULL,
    EndsAt   DATETIMEOFFSET(7) NULL
);
INSERT #TargetAssignments SELECT Id, CardId, UserId, StartsAt, EndsAt FROM [$(TargetServer)].[$(TargetDatabase)].[attendance].[CardAssignments];

IF OBJECT_ID('tempdb..#TargetFile') IS NOT NULL DROP TABLE #TargetFile;
SELECT Id, LastImportId INTO #TargetFile
FROM [$(TargetServer)].[$(TargetDatabase)].[attendance].[PunchFiles]
WHERE FileName = N'$(LegacyFileName)';

-- 只讀可能撞鍵的刷卡：同機器代號、落在舊資料的出勤日範圍內。上線後排程匯入的同一天檔案也會在這裡被認出。
DECLARE @first DATE, @last DATE;
SELECT @first = MIN(CONVERT(DATE, PunchedAt)), @last = MAX(CONVERT(DATE, PunchedAt)) FROM #Clock WHERE Problem IS NULL;
IF OBJECT_ID('tempdb..#TargetPunchKeys') IS NOT NULL DROP TABLE #TargetPunchKeys;
CREATE TABLE #TargetPunchKeys (
    CardNumber  NVARCHAR(32) COLLATE Chinese_Taiwan_Stroke_CI_AS NOT NULL,
    PunchedAt   DATETIMEOFFSET(7) NOT NULL,
    PunchFileId UNIQUEIDENTIFIER NOT NULL
);
IF @first IS NOT NULL
    INSERT #TargetPunchKeys
    SELECT CardNumber, PunchedAt, PunchFileId
    FROM [$(TargetServer)].[$(TargetDatabase)].[attendance].[Punches]
    WHERE MachineCode = N'$(LegacyMachineCode)' AND AttendanceDate BETWEEN @first AND @last;
-- datetimeoffset 以瞬間比較，與唯一索引一致。
CREATE CLUSTERED INDEX cx_TargetPunchKeys ON #TargetPunchKeys (CardNumber, PunchedAt);
GO

-- ============================================================================
-- 3. 還原持有期間
--    EmpClock.EmpNo 是匯入當下的持有人（換人時舊系統改寫為新職號），所以它就是歸戶的答案。
--    同一張卡依時間排序，職號連續相同的一段刷卡＝一段持有（gaps and islands）。
--    卡沒有輪回同一人時，結果等於規格的「（卡號、職號）最早與最晚刷卡」；A→B→A 輪回則切成三段，避免 A 的一段蓋住 B。
--    UNKNOWN_ 也是一段：它把前後隔開但不轉，那段刷卡留在未歸戶。
-- ============================================================================
IF OBJECT_ID('tempdb..#Runs') IS NOT NULL DROP TABLE #Runs;
CREATE TABLE #Runs (
    CardNumber NVARCHAR(32) COLLATE Chinese_Taiwan_Stroke_CI_AS NOT NULL,
    RunNo      INT NOT NULL,
    EmpNo      VARCHAR(50) COLLATE Chinese_Taiwan_Stroke_CI_AS NOT NULL,
    IsUnknown  BIT NOT NULL,
    FirstAt    DATETIMEOFFSET(7) NOT NULL,
    LastAt     DATETIMEOFFSET(7) NOT NULL,
    PunchCount INT NOT NULL,
    PRIMARY KEY (CardNumber, RunNo)
);
WITH x AS (
    SELECT CardNumber, EmpNo, PunchedAt,
           CASE WHEN LAG(EmpNo) OVER (PARTITION BY CardNumber ORDER BY PunchedAt) = EmpNo THEN 0 ELSE 1 END AS NewRun
    FROM #Clock WHERE Problem IS NULL
), y AS (
    SELECT CardNumber, EmpNo, PunchedAt, SUM(NewRun) OVER (PARTITION BY CardNumber ORDER BY PunchedAt ROWS UNBOUNDED PRECEDING) AS RunNo
    FROM x
)
INSERT #Runs
SELECT CardNumber, RunNo, MIN(EmpNo), CASE WHEN MIN(EmpNo) LIKE 'UNKNOWN[_]%' THEN 1 ELSE 0 END, MIN(PunchedAt), MAX(PunchedAt), COUNT(*)
FROM y GROUP BY CardNumber, RunNo;

-- 現行對應：EmpCard 上目前掛著這張卡的人（UNKNOWN_ 與空卡號除外）。舊系統不保證卡號唯一，兩人以上掛同一卡時不採用。
DECLARE @control NVARCHAR(20) = N'%[' + NCHAR(1) + N'-' + NCHAR(31) + NCHAR(127) + N'-' + NCHAR(159) + N']%';
IF OBJECT_ID('tempdb..#CurrentCard') IS NOT NULL DROP TABLE #CurrentCard;
SELECT CardNumber,
       CASE WHEN COUNT(*) = 1 THEN MIN(EmpNo) END AS EmpNo,
       CASE WHEN COUNT(*) = 1 THEN MIN(UpdatedAt) END AS UpdatedAt,
       COUNT(*) AS Holders,
       CASE WHEN LEN(CardNumber) > 32 THEN N'卡號超過 32 字'
            WHEN PATINDEX(@control, CardNumber COLLATE Latin1_General_BIN2) > 0 THEN N'卡號含控制字元' END AS Problem
INTO #CurrentCard
FROM #EmpCard
WHERE CardNumber <> N'' AND EmpNo NOT LIKE 'UNKNOWN[_]%'
GROUP BY CardNumber;

-- 候選持有期間。
--   一般段：[該段第一筆刷卡, 該段最後一筆刷卡＋1 秒)，不接到下一段的開始：兩段之間若有 UNKNOWN_ 刷卡，接起來會把它們歸給前一人。
--   卡上最後一段且就是現行對應的人：EndsAt 為空（持有中）。
--   現行對應的人在這張卡上沒有刷卡（新發卡還沒刷、或接手後還沒刷）：從 EmpCard.UpdateDTTM 起持有中，
--   但不早於前一段的結束；UpdateDTTM 是最後異動時間，不早於實際發卡，之後日後匯入的刷卡就會歸戶。
IF OBJECT_ID('tempdb..#Cand') IS NOT NULL DROP TABLE #Cand;
CREATE TABLE #Cand (
    CandId      INT IDENTITY PRIMARY KEY,
    CardNumber  NVARCHAR(32) COLLATE Chinese_Taiwan_Stroke_CI_AS NOT NULL,
    RunNo       INT NULL,
    EmpNo       VARCHAR(50) COLLATE Chinese_Taiwan_Stroke_CI_AS NOT NULL,
    StartsAt    DATETIMEOFFSET(7) NOT NULL,
    EndsAt      DATETIMEOFFSET(7) NULL,
    PunchCount  INT NOT NULL,
    LastPunchAt DATETIMEOFFSET(7) NULL,
    CardId      UNIQUEIDENTIFIER NULL,
    UserId      UNIQUEIDENTIFIER NULL,
    HolderName  NVARCHAR(120) COLLATE Chinese_Taiwan_Stroke_CI_AS NULL,
    Status      NVARCHAR(16) COLLATE Chinese_Taiwan_Stroke_CI_AS NULL,
    Reason      NVARCHAR(100) COLLATE Chinese_Taiwan_Stroke_CI_AS NULL,
    ConflictId  UNIQUEIDENTIFIER NULL,
    NewId       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID()
);
INSERT #Cand (CardNumber, RunNo, EmpNo, StartsAt, EndsAt, PunchCount, LastPunchAt)
SELECT r.CardNumber, r.RunNo, r.EmpNo, r.FirstAt,
       CASE WHEN n.RunNo IS NULL AND cur.EmpNo = r.EmpNo AND cur.Problem IS NULL THEN NULL
            WHEN n.FirstAt < DATEADD(SECOND, 1, r.LastAt) THEN n.FirstAt
            ELSE DATEADD(SECOND, 1, r.LastAt) END,
       r.PunchCount, r.LastAt
FROM #Runs AS r
LEFT JOIN #Runs AS n ON n.CardNumber = r.CardNumber AND n.RunNo = r.RunNo + 1
LEFT JOIN #CurrentCard AS cur ON cur.CardNumber = r.CardNumber
WHERE r.IsUnknown = 0;

INSERT #Cand (CardNumber, RunNo, EmpNo, StartsAt, EndsAt, PunchCount, LastPunchAt)
SELECT cc.CardNumber, NULL, cc.EmpNo,
       CASE WHEN l.LastAt IS NOT NULL AND cc.UpdatedAt < DATEADD(SECOND, 1, l.LastAt) THEN DATEADD(SECOND, 1, l.LastAt) ELSE cc.UpdatedAt END,
       NULL, 0, NULL
FROM #CurrentCard AS cc
OUTER APPLY (SELECT TOP (1) r.EmpNo, r.LastAt FROM #Runs AS r WHERE r.CardNumber = cc.CardNumber ORDER BY r.RunNo DESC) AS l
WHERE cc.Holders = 1 AND cc.Problem IS NULL AND (l.EmpNo IS NULL OR l.EmpNo <> cc.EmpNo);
GO

-- ============================================================================
-- 4. 職號 → 使用者：EmpCard.Account（公文帳號 Sys_User.ID）→ Sys_User.ACCOUNT → Users.AdAccount
--    比對與程式的 GdAccounts 相同：去前後空白、不分大小寫。已刪除的使用者照樣對上：歷史刷卡仍屬於他。
-- ============================================================================
IF OBJECT_ID('tempdb..#Emp') IS NOT NULL DROP TABLE #Emp;
CREATE TABLE #Emp (
    EmpNo       VARCHAR(50)   COLLATE Chinese_Taiwan_Stroke_CI_AS NOT NULL PRIMARY KEY,
    EmpName     NVARCHAR(50)  COLLATE Chinese_Taiwan_Stroke_CI_AS NULL,
    Source      NVARCHAR(16)  COLLATE Chinese_Taiwan_Stroke_CI_AS NULL,
    GdId        NVARCHAR(50)  COLLATE Chinese_Taiwan_Stroke_CI_AS NULL,
    AdAccount   NVARCHAR(256) COLLATE Chinese_Taiwan_Stroke_CI_AS NULL,
    SysUserRows INT NULL,
    UserId      UNIQUEIDENTIFIER NULL,
    UserDeleted BIT NULL,
    Reason      NVARCHAR(60)  COLLATE Chinese_Taiwan_Stroke_CI_AS NULL
);
INSERT #Emp (EmpNo) SELECT DISTINCT EmpNo FROM #Cand;

UPDATE e SET EmpName = c.EmpName, GdId = c.GdId, Source = N'EmpCard'
FROM #Emp AS e
CROSS APPLY (SELECT TOP (1) EmpName, GdId FROM #EmpCard AS c WHERE c.EmpNo = e.EmpNo ORDER BY c.UpdatedAt DESC) AS c;

UPDATE e SET EmpName = l.EmpName, GdId = l.GdId, Source = N'EmpCardLog'
FROM #Emp AS e
CROSS APPLY (SELECT TOP (1) EmpName, GdId FROM #EmpCardLog AS l WHERE l.EmpNo = e.EmpNo
             ORDER BY CASE WHEN l.GdId <> N'' THEN 0 ELSE 1 END, l.UpdateDTTM DESC) AS l
WHERE e.Source IS NULL;

UPDATE e SET SysUserRows = s.Rows, AdAccount = CASE WHEN s.Rows = 1 THEN s.AdAccount END
FROM #Emp AS e
CROSS APPLY (SELECT COUNT(DISTINCT NULLIF(AdAccount, N'')) AS Rows, MAX(AdAccount) AS AdAccount, COUNT(*) AS Total
             FROM #SysUser AS s WHERE s.GdId = e.GdId) AS s
WHERE e.GdId <> N'' AND s.Total > 0;

UPDATE e SET UserId = u.Id, UserDeleted = u.IsDeleted
FROM #Emp AS e
JOIN #TargetUsers AS u ON u.AdAccount = e.AdAccount
WHERE e.SysUserRows = 1;

UPDATE #Emp SET Reason =
    CASE WHEN Source IS NULL THEN N'EmpCard、EmpCardLog 都沒有此職號'
         WHEN GdId = N'' THEN Source + N' 的公文帳號（Account）空白'
         WHEN SysUserRows IS NULL THEN N'Sys_User 沒有此公文帳號（ID）'
         WHEN SysUserRows = 0 THEN N'Sys_User.ACCOUNT（AD 帳號）空白'
         WHEN SysUserRows > 1 THEN N'Sys_User 同一 ID 有多個 ACCOUNT'
         ELSE N'Users 沒有此 AD 帳號' END
WHERE UserId IS NULL;
GO

-- ============================================================================
-- 5. 卡片與候選期間的狀態
--    Cards：舊系統認得持有人的卡（刷卡段或現行對應）都登錄為員工卡；只出現在 UNKNOWN_ 的卡不登錄，
--    它們以未登錄卡號出現在未歸戶清單（ADR 0018），HR 登錄時才決定是員工卡還是訪客卡。
-- ============================================================================
IF OBJECT_ID('tempdb..#NewCards') IS NOT NULL DROP TABLE #NewCards;
SELECT s.CardNumber, NEWID() AS Id
INTO #NewCards
FROM (SELECT CardNumber FROM #Cand UNION SELECT CardNumber FROM #CurrentCard WHERE Problem IS NULL) AS s
WHERE NOT EXISTS (SELECT 1 FROM #TargetCards AS t WHERE t.CardNumber = s.CardNumber);

UPDATE c SET CardId = COALESCE(t.Id, n.Id)
FROM #Cand AS c
LEFT JOIN #TargetCards AS t ON t.CardNumber = c.CardNumber
LEFT JOIN #NewCards AS n ON n.CardNumber = c.CardNumber;

UPDATE c SET UserId = e.UserId,
    HolderName = CASE WHEN e.UserId IS NULL AND N'$(UnmatchedAsHolderName)' = N'1' THEN NULLIF(LTRIM(RTRIM(e.EmpName)), N'') END,
    Reason = e.Reason
FROM #Cand AS c JOIN #Emp AS e ON e.EmpNo = c.EmpNo;

-- 已存在：同一張卡、同一個開始時間就當作先前轉過（不比持有人與結束時間，不覆寫 HR 之後的修改）。
-- 重疊：與目標既有期間（HR 在新系統建的、或修改過開始時間的舊轉入）重疊就不寫，列清單人工處理。
DECLARE @max DATETIMEOFFSET(7) = '9999-12-31T23:59:59.9999999+00:00';
UPDATE c SET Status =
    CASE WHEN c.UserId IS NULL AND c.HolderName IS NULL THEN N'unmatched'
         WHEN EXISTS (SELECT 1 FROM #TargetCards AS t WHERE t.Id = c.CardId AND t.Kind = N'visitor') THEN N'visitor'
         WHEN EXISTS (SELECT 1 FROM #TargetAssignments AS a WHERE a.CardId = c.CardId AND a.StartsAt = c.StartsAt) THEN N'exists'
         WHEN o.Id IS NOT NULL THEN N'overlap'
         ELSE N'new' END,
    ConflictId = o.Id
FROM #Cand AS c
OUTER APPLY (SELECT TOP (1) a.Id FROM #TargetAssignments AS a
             WHERE a.CardId = c.CardId AND a.StartsAt < ISNULL(c.EndsAt, @max) AND c.StartsAt < ISNULL(a.EndsAt, @max)
             ORDER BY a.StartsAt) AS o;

-- 刷卡：扣掉目標已有的鍵（卡號、時間；機器代號已在讀取時篩過），依時間編號以便分批。
IF OBJECT_ID('tempdb..#PunchStage') IS NOT NULL DROP TABLE #PunchStage;
CREATE TABLE #PunchStage (
    RowNo          INT IDENTITY PRIMARY KEY,
    CardNumber     NVARCHAR(32) COLLATE Chinese_Taiwan_Stroke_CI_AS NOT NULL,
    PunchedAt      DATETIMEOFFSET(7) NOT NULL,
    AttendanceDate DATE NOT NULL
);
INSERT #PunchStage (CardNumber, PunchedAt, AttendanceDate)
SELECT c.CardNumber, c.PunchedAt, CONVERT(DATE, c.PunchedAt)
FROM #Clock AS c
WHERE c.Problem IS NULL
  AND NOT EXISTS (SELECT 1 FROM #TargetPunchKeys AS k WHERE k.CardNumber = c.CardNumber AND k.PunchedAt = c.PunchedAt)
ORDER BY c.PunchedAt, c.CardNumber;
GO

-- ============================================================================
-- 6. 報表（預覽與寫入都會輸出；寫入模式下是寫入前的計畫）
-- ============================================================================
DECLARE @batch INT = $(BatchSize);
DECLARE @stage INT = (SELECT COUNT(*) FROM #PunchStage);
SELECT N'摘要' AS [清單], x.[項目], x.[數量]
FROM (VALUES
    (1, N'來源刷卡（EmpClock）', (SELECT COUNT(*) FROM #Clock)),
    (2, N'無法轉入的刷卡', (SELECT COUNT(*) FROM #Clock WHERE Problem IS NOT NULL)),
    (3, N'UNKNOWN_ 刷卡（照轉、不建持有期間）', (SELECT ISNULL(SUM(PunchCount), 0) FROM #Runs WHERE IsUnknown = 1)),
    (4, N'目標已有的刷卡（略過）', (SELECT COUNT(*) FROM #Clock AS c WHERE c.Problem IS NULL AND EXISTS (SELECT 1 FROM #TargetPunchKeys AS k WHERE k.CardNumber = c.CardNumber AND k.PunchedAt = c.PunchedAt))),
    (5, N'將新增 Punches', @stage),
    (6, N'刷卡寫入批次數', (@stage + @batch - 1) / @batch),
    (7, N'將新增 Cards', (SELECT COUNT(*) FROM #NewCards)),
    (8, N'將新增 CardAssignments', (SELECT COUNT(*) FROM #Cand WHERE Status = N'new')),
    (9, N'  其中只有姓名（UnmatchedAsHolderName）', (SELECT COUNT(*) FROM #Cand WHERE Status = N'new' AND UserId IS NULL)),
    (10, N'已存在的持有期間（略過）', (SELECT COUNT(*) FROM #Cand WHERE Status = N'exists')),
    (11, N'對不上使用者的持有期間（不寫）', (SELECT COUNT(*) FROM #Cand WHERE Status = N'unmatched')),
    (12, N'重疊或訪客卡的持有期間（不寫）', (SELECT COUNT(*) FROM #Cand WHERE Status IN (N'overlap', N'visitor')))
) AS x(n, [項目], [數量])
ORDER BY x.n;

-- 對不上的人：以（職號、卡號）彙總；處理方式見操作文件。
SELECT N'對不上的人' AS [清單], c.EmpNo AS [職號], e.EmpName AS [姓名], e.GdId AS [公文帳號], e.AdAccount AS [AD帳號],
       c.CardNumber AS [卡號], SUM(c.PunchCount) AS [刷卡筆數], MIN(c.StartsAt) AS [首次刷卡或起始], MAX(c.LastPunchAt) AS [末次刷卡],
       e.Reason AS [原因], CASE WHEN MAX(c.HolderName) IS NOT NULL THEN N'以姓名寫入' ELSE N'不寫' END AS [處理]
FROM #Cand AS c JOIN #Emp AS e ON e.EmpNo = c.EmpNo
WHERE e.UserId IS NULL
GROUP BY c.EmpNo, e.EmpName, e.GdId, e.AdAccount, c.CardNumber, e.Reason
ORDER BY e.Reason, c.EmpNo, c.CardNumber;

-- 無法寫入的持有期間：重疊、訪客卡、舊系統一卡多人。
SELECT N'無法寫入的持有期間' AS [清單], x.[卡號], x.[職號], x.[姓名], x.[起始], x.[結束], x.[原因], x.[衝突的既有期間]
FROM (
    SELECT c.CardNumber AS [卡號], c.EmpNo AS [職號], e.EmpName AS [姓名], c.StartsAt AS [起始], c.EndsAt AS [結束],
           CASE c.Status WHEN N'overlap' THEN N'與目標既有持有期間重疊' ELSE N'目標已登錄為訪客卡' END AS [原因],
           CASE WHEN a.Id IS NOT NULL THEN CONVERT(NVARCHAR(36), a.Id) + N' ' + CONVERT(NVARCHAR(40), a.StartsAt, 126) + N' ~ ' + ISNULL(CONVERT(NVARCHAR(40), a.EndsAt, 126), N'持有中') END AS [衝突的既有期間]
    FROM #Cand AS c
    JOIN #Emp AS e ON e.EmpNo = c.EmpNo
    LEFT JOIN #TargetAssignments AS a ON a.Id = c.ConflictId
    WHERE c.Status IN (N'overlap', N'visitor')
    UNION ALL
    SELECT CardNumber, NULL, NULL, NULL, NULL,
           CASE WHEN Problem IS NOT NULL THEN N'EmpCard 的卡號無效：' + Problem
                ELSE N'EmpCard 有 ' + CONVERT(NVARCHAR(10), Holders) + N' 人掛同一卡號，不建持有中的期間' END, NULL
    FROM #CurrentCard WHERE Holders > 1 OR Problem IS NOT NULL
) AS x
ORDER BY x.[卡號], x.[起始];

SELECT N'無法轉入的刷卡' AS [清單], CardRaw AS [原始卡號], Problem AS [原因], COUNT(*) AS [筆數], MIN(ClockDTTM) AS [首次], MAX(ClockDTTM) AS [末次]
FROM #Clock WHERE Problem IS NOT NULL
GROUP BY CardRaw, Problem
ORDER BY Problem, CardRaw;

-- 注意：會寫入，但值得 HR 看一眼。
SELECT N'注意' AS [清單], x.[卡號], x.[職號], x.[說明]
FROM (
    SELECT CardNumber AS [卡號], EmpNo AS [職號], N'同一人在這張卡上有 ' + CONVERT(NVARCHAR(10), COUNT(*)) + N' 段不連續的持有（中間有別人或 UNKNOWN_ 刷卡）' AS [說明]
    FROM #Runs WHERE IsUnknown = 0 GROUP BY CardNumber, EmpNo HAVING COUNT(*) > 1
    UNION ALL
    SELECT DISTINCT c.CardNumber, c.EmpNo, N'對到已刪除的使用者，仍轉入'
    FROM #Cand AS c JOIN #Emp AS e ON e.EmpNo = c.EmpNo WHERE e.UserDeleted = 1 AND c.Status = N'new'
    UNION ALL
    SELECT c.CardNumber, c.EmpNo, N'現行對應但沒有刷卡，從 EmpCard 最後異動時間起算持有中'
    FROM #Cand AS c WHERE c.RunNo IS NULL AND c.Status = N'new'
) AS x
ORDER BY x.[卡號], x.[職號];

SELECT N'將新增的持有期間' AS [清單], c.CardNumber AS [卡號], c.EmpNo AS [職號], ISNULL(u.DisplayName, c.HolderName) AS [持有人],
       u.AdAccount AS [AD帳號], c.StartsAt AS [起始], c.EndsAt AS [結束（不含）], c.PunchCount AS [刷卡筆數]
FROM #Cand AS c LEFT JOIN #TargetUsers AS u ON u.Id = c.UserId
WHERE c.Status = N'new'
ORDER BY c.CardNumber, c.StartsAt;
GO

-- ============================================================================
-- 7. 寫入（Apply 1）：Cards → CardAssignments → PunchFiles → Punches（分批）→ PunchImports
--    每個 INSERT 各自提交；中途失敗時重跑即可，第 2、5 節會把已寫入的排除。
--    寫入期間新系統若有人改同一張卡的持有期間或匯入同一天的檔案，可能撞唯一索引而失敗，重跑即可；
--    建議轉移時把 Attendance:Import:Enabled 設為 false。
-- ============================================================================
IF N'$(Apply)' <> N'1'
BEGIN
    PRINT N'預覽完成，未寫入。確認清單後把 Apply 設為 1 再執行。';
    RETURN;
END;

DECLARE @now DATETIMEOFFSET(7) = SWITCHOFFSET(SYSDATETIMEOFFSET(), '+00:00');
DECLARE @runStart DATETIMEOFFSET(7) = (SELECT StartedAt FROM #RunInfo);
DECLARE @n INT;

INSERT INTO [$(TargetServer)].[$(TargetDatabase)].[attendance].[Cards] (Id, CardNumber, Kind, Note, CreatedAt)
SELECT Id, CardNumber, N'employee', N'舊系統 ADAuthentication 轉入', @now FROM #NewCards;
SET @n = @@ROWCOUNT;
RAISERROR(N'Cards 新增 %d 筆', 0, 1, @n) WITH NOWAIT;

INSERT INTO [$(TargetServer)].[$(TargetDatabase)].[attendance].[CardAssignments] (Id, CardId, UserId, HolderName, StartsAt, EndsAt, Note, CreatedAt)
SELECT NewId, CardId, UserId, HolderName, StartsAt, EndsAt,
       N'舊系統轉入（職號 ' + EmpNo + CASE WHEN RunNo IS NULL THEN N'，現行對應、轉入時尚無刷卡' ELSE N'' END + N'）', @now
FROM #Cand WHERE Status = N'new';
SET @n = @@ROWCOUNT;
RAISERROR(N'CardAssignments 新增 %d 筆', 0, 1, @n) WITH NOWAIT;

-- 舊系統轉入的刷卡掛在一個固定檔名的 PunchFiles 列下（Punches.PunchFileId 必填）。
-- 這個檔永遠不會被重匯，所以這些列不會被刪；日後匯入的每日檔遇到同一筆刷卡會視為已屬於它而略過。
DECLARE @total INT = (SELECT COUNT(*) FROM #PunchStage);
DECLARE @fileId UNIQUEIDENTIFIER, @lastImportId UNIQUEIDENTIFIER;
SELECT @fileId = Id, @lastImportId = LastImportId FROM #TargetFile;
IF @fileId IS NULL AND @total > 0
BEGIN
    SET @fileId = NEWID();
    INSERT INTO [$(TargetServer)].[$(TargetDatabase)].[attendance].[PunchFiles] (Id, FileName, ImportedModifiedAt, ImportedSize, LastImportId)
    VALUES (@fileId, N'$(LegacyFileName)', NULL, NULL, NULL);
END;

DECLARE @batch INT = $(BatchSize), @from INT = 1, @inserted INT = 0, @done INT, @pct INT, @elapsed INT, @remaining INT;
DECLARE @clock DATETIME2 = SYSUTCDATETIME();
WHILE @from <= @total
BEGIN
    INSERT INTO [$(TargetServer)].[$(TargetDatabase)].[attendance].[Punches] (PunchFileId, CardNumber, PunchedAt, AttendanceDate, MachineCode, EventCode)
    SELECT @fileId, CardNumber, PunchedAt, AttendanceDate, N'$(LegacyMachineCode)', N'$(LegacyEventCode)'
    FROM #PunchStage WHERE RowNo BETWEEN @from AND @from + @batch - 1;
    SET @inserted += @@ROWCOUNT;
    SET @from += @batch;
    SET @done = CASE WHEN @from - 1 > @total THEN @total ELSE @from - 1 END;
    SET @pct = CONVERT(INT, 100.0 * @done / @total);
    SET @elapsed = DATEDIFF(SECOND, @clock, SYSUTCDATETIME());
    SET @remaining = CONVERT(INT, 1.0 * @elapsed * (@total - @done) / NULLIF(@done, 0));
    RAISERROR(N'Punches %d / %d（%d%%），已用 %d 秒，預估剩 %d 秒', 0, 1, @done, @total, @pct, @elapsed, @remaining) WITH NOWAIT;
END;
RAISERROR(N'Punches 新增 %d 筆', 0, 1, @inserted) WITH NOWAIT;

-- 匯入紀錄只增不改：這次有寫入、或上次寫完刷卡卻沒來得及記錄時才新增一列。
-- PunchCount 是這個檔擁有的刷卡總數（與排程匯入的定義相同）；沒有來源檔，所以版本記為開始時間與大小 0。
IF @fileId IS NOT NULL AND (@inserted > 0 OR @lastImportId IS NULL)
BEGIN
    DECLARE @importId UNIQUEIDENTIFIER = NEWID();
    DECLARE @owned INT = @inserted + (SELECT COUNT(*) FROM #TargetPunchKeys WHERE PunchFileId = @fileId);
    DECLARE @finished DATETIMEOFFSET(7) = SWITCHOFFSET(SYSDATETIMEOFFSET(), '+00:00');
    INSERT INTO [$(TargetServer)].[$(TargetDatabase)].[attendance].[PunchImports]
        (Id, PunchFileId, TriggerKind, RequestedById, StartedAt, FinishedAt, SourceModifiedAt, SourceSize, PunchCount, Status, ErrorMessage)
    VALUES (@importId, @fileId, N'manual', NULL, @runStart, @finished, @runStart, 0, @owned, N'succeeded', NULL);
    UPDATE f SET LastImportId = @importId, ImportedModifiedAt = @runStart, ImportedSize = 0
    FROM [$(TargetServer)].[$(TargetDatabase)].[attendance].[PunchFiles] AS f
    WHERE f.Id = @fileId;
    RAISERROR(N'PunchImports 新增 1 筆（檔案擁有 %d 筆刷卡）', 0, 1, @owned) WITH NOWAIT;
END;

PRINT N'寫入完成。再以 Apply 0 執行一次，摘要的各項「將新增」應為 0。';
GO
