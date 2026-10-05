using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiNexus.BuildingBlocks.Migrations
{
    /// <inheritdoc />
    public partial class ManagedIdentitiesAndDescriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "WebSearches",
                schema: "inference",
                comment: "使用者明確啟用的網路搜尋、冪等識別、結果與費用。");

            migrationBuilder.AlterTable(
                name: "Users",
                schema: "identity",
                comment: "使用者身分、AD SID 綁定、可用登入方式與工作階段撤銷版本；不保存 AD 密碼。");

            migrationBuilder.AlterTable(
                name: "UserRoles",
                schema: "access",
                comment: "使用者與角色的分派關聯。");

            migrationBuilder.AlterTable(
                name: "UserPreferences",
                schema: "identity",
                comment: "使用者個人外觀、閱讀、對話操作與通知偏好；不含服務密鑰。");

            migrationBuilder.AlterTable(
                name: "SourceReferences",
                schema: "content",
                comment: "外部來源匯入的識別、來源版本與匯入時間。");

            migrationBuilder.AlterTable(
                name: "ShareRecipients",
                schema: "collaboration",
                comment: "分享的具名收件人；與原資源 ACL 分開判定。");

            migrationBuilder.AlterTable(
                name: "ShareLinks",
                schema: "collaboration",
                comment: "分享版本快照、有效期限、附件選項及撤銷狀態。");

            migrationBuilder.AlterTable(
                name: "RunEvents",
                schema: "inference",
                comment: "生成事件的有序 SSE 重播紀錄；完整生成狀態以 GenerationRuns 為準。");

            migrationBuilder.AlterTable(
                name: "Roles",
                schema: "access",
                comment: "可分派給使用者的角色；停用後不再提供有效授權。");

            migrationBuilder.AlterTable(
                name: "RoleGroups",
                schema: "access",
                comment: "角色所加入的功能群組，集中管理功能及模型政策。");

            migrationBuilder.AlterTable(
                name: "RoleGroupRoles",
                schema: "access",
                comment: "角色與功能群組的授權關聯。");

            migrationBuilder.AlterTable(
                name: "RoleGroupFeatures",
                schema: "access",
                comment: "功能群組與功能的授權關聯；有效功能取聯集。");

            migrationBuilder.AlterTable(
                name: "Resources",
                schema: "collaboration",
                comment: "共用資源的擁有者、種類、階層及版本；作為資料 ACL 邊界。");

            migrationBuilder.AlterTable(
                name: "ResourceMembers",
                schema: "collaboration",
                comment: "資源對具名使用者授予的閱讀或編輯權限。");

            migrationBuilder.AlterTable(
                name: "ResourceGroups",
                schema: "collaboration",
                comment: "資源對功能群組授予的唯讀權限。");

            migrationBuilder.AlterTable(
                name: "ResourceAttachments",
                schema: "attachments",
                comment: "知識庫或專案資源與附件的引用關聯。");

            migrationBuilder.AlterTable(
                name: "RepositoryImports",
                schema: "knowledge",
                comment: "程式庫文件匯入所固定的主機、repository、commit 與檔案路徑。");

            migrationBuilder.AlterTable(
                name: "RepositoryConnections",
                schema: "workspace",
                comment: "使用者個人的 Gitea 連線及 Data Protection 保護的存取 token。");

            migrationBuilder.AlterTable(
                name: "PromptTemplates",
                schema: "library",
                comment: "使用者私人提示詞範本。");

            migrationBuilder.AlterTable(
                name: "ProjectTemplates",
                schema: "projects",
                comment: "專案建立範本與預設指令。");

            migrationBuilder.AlterTable(
                name: "Projects",
                schema: "projects",
                comment: "專案資源、共用指令及範本版本。");

            migrationBuilder.AlterTable(
                name: "ModelProfiles",
                schema: "inference",
                comment: "核准模型的能力、上下文與輸出限制。");

            migrationBuilder.AlterTable(
                name: "ModelPrices",
                schema: "inference",
                comment: "依供應商、模型、幣別及成本類型保存的不可變價格版本。");

            migrationBuilder.AlterTable(
                name: "ModelInvocations",
                schema: "inference",
                comment: "文字、OCR、embedding 等模型呼叫的狀態與實際用量。");

            migrationBuilder.AlterTable(
                name: "ModelCharges",
                schema: "inference",
                comment: "各呼叫當時的價格與用量快照；未知費用保持空值。");

            migrationBuilder.AlterTable(
                name: "Messages",
                schema: "conversations",
                comment: "對話訊息樹；提問、回答、重新生成與編輯保留各版本。");

            migrationBuilder.AlterTable(
                name: "MessageFeedback",
                schema: "quality",
                comment: "使用者對 AI 回答的私人評分與意見。");

            migrationBuilder.AlterTable(
                name: "MessageCitations",
                schema: "knowledge",
                comment: "回答生成當時的知識引用、文件頁碼與摘要快照。");

            migrationBuilder.AlterTable(
                name: "MessageAttachments",
                schema: "attachments",
                comment: "訊息與附件的關聯及呈現順序。");

            migrationBuilder.AlterTable(
                name: "GroupModelPolicies",
                schema: "access",
                comment: "功能群組的模型白名單、每日生成及附件空間限制。");

            migrationBuilder.AlterTable(
                name: "GenerationRuns",
                schema: "inference",
                comment: "聊天生成的持久狀態、冪等請求、執行租約、回答及用量。");

            migrationBuilder.AlterTable(
                name: "Features",
                schema: "access",
                comment: "模組註冊的功能入口、顯示名稱、路由及啟用狀態。");

            migrationBuilder.AlterTable(
                name: "EvaluationSets",
                schema: "quality",
                comment: "評測題庫、固定測試案例與版本。");

            migrationBuilder.AlterTable(
                name: "EvaluationRuns",
                schema: "quality",
                comment: "評測執行的題庫與模型設定快照、背景工作與狀態。");

            migrationBuilder.AlterTable(
                name: "EvaluationResults",
                schema: "quality",
                comment: "各案例／模型組合的輸出、指標、用量與人工覆核結果。");

            migrationBuilder.AlterTable(
                name: "Documents",
                schema: "knowledge",
                comment: "知識庫文件的原始附件、分析／索引狀態與 embedding profile。");

            migrationBuilder.AlterTable(
                name: "DocumentPages",
                schema: "knowledge",
                comment: "文件逐頁擷取的文字、頁碼與 OCR 結果。");

            migrationBuilder.AlterTable(
                name: "Conversations",
                schema: "conversations",
                comment: "使用者私人對話、目前訊息分支、收藏封存與自訂指令。");

            migrationBuilder.AlterTable(
                name: "ConversationLabels",
                schema: "conversations",
                comment: "使用者對話的分類標籤。");

            migrationBuilder.AlterTable(
                name: "ConversationCollections",
                schema: "knowledge",
                comment: "對話選定的知識庫來源關聯。");

            migrationBuilder.AlterTable(
                name: "Collections",
                schema: "knowledge",
                comment: "知識庫的資料資源關聯及索引資訊。");

            migrationBuilder.AlterTable(
                name: "Chunks",
                schema: "knowledge",
                comment: "知識檢索片段、頁碼、摘要與向量；查詢先套用資料 ACL。");

            migrationBuilder.AlterTable(
                name: "BackgroundJobs",
                schema: "operations",
                comment: "文件索引與評測等背景工作的租約、進度、重試與取消狀態。");

            migrationBuilder.AlterTable(
                name: "AuditEvents",
                schema: "operations",
                comment: "操作與管理異動稽核；保存實際管理者及有效身分，不記錄密碼或私密內容。");

            migrationBuilder.AlterTable(
                name: "Attachments",
                schema: "attachments",
                comment: "使用者附件的原始二進位資料、擷取文字及保留狀態。");

            migrationBuilder.AlterTable(
                name: "Artifacts",
                schema: "content",
                comment: "使用者保存的成果文件與目前版本。");

            migrationBuilder.AlterTable(
                name: "ArtifactRevisions",
                schema: "content",
                comment: "成果文件不可變版本、內容與作者。");

            migrationBuilder.AlterTable(
                name: "AdministratorBootstraps",
                schema: "access",
                comment: "管理者首次啟動授權的永久標記，防止撤銷後重新授權。");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "inference",
                table: "WebSearches",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                comment: "業務執行狀態。",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<Guid>(
                name: "RunId",
                schema: "inference",
                table: "WebSearches",
                type: "uniqueidentifier",
                nullable: true,
                comment: "關聯生成或評測執行的識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ResultsJson",
                schema: "inference",
                table: "WebSearches",
                type: "nvarchar(max)",
                nullable: false,
                comment: "搜尋結果的 JSON 快照。",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "RequestHash",
                schema: "inference",
                table: "WebSearches",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                comment: "請求內容指紋，用於辨識冪等識別碼衝突。",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerId",
                schema: "inference",
                table: "WebSearches",
                type: "uniqueidentifier",
                nullable: false,
                comment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "IdempotencyKey",
                schema: "inference",
                table: "WebSearches",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                comment: "擁有者範圍內的冪等請求識別，避免重試重複處理。",
                oldClrType: typeof(string),
                oldType: "nvarchar(80)",
                oldMaxLength: 80);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "inference",
                table: "WebSearches",
                type: "datetimeoffset",
                nullable: false,
                comment: "資料建立時間，採 UTC offset。",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<Guid>(
                name: "ConversationId",
                schema: "inference",
                table: "WebSearches",
                type: "uniqueidentifier",
                nullable: false,
                comment: "關聯對話的識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "inference",
                table: "WebSearches",
                type: "uniqueidentifier",
                nullable: false,
                comment: "資料的主鍵識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "Sid",
                schema: "identity",
                table: "Users",
                type: "nvarchar(184)",
                maxLength: 184,
                nullable: false,
                comment: "AD 的不可變 SID；尚未綁定 AD 的手動帳號使用 managed: 識別碼。",
                oldClrType: typeof(string),
                oldType: "nvarchar(184)",
                oldMaxLength: 184);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "LastSeenAt",
                schema: "identity",
                table: "Users",
                type: "datetimeoffset",
                nullable: false,
                comment: "使用者最近登入／活動時間，採 UTC offset。",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                schema: "identity",
                table: "Users",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                comment: "使用者或模型的介面顯示名稱。",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "Account",
                schema: "identity",
                table: "Users",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                comment: "登入身分顯示帳號；AD 連結後保存目錄提供的帳號。",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "identity",
                table: "Users",
                type: "uniqueidentifier",
                nullable: false,
                comment: "資料的主鍵識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "AdAccount",
                schema: "identity",
                table: "Users",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                comment: "預先配置的 AD 帳號正規化值；唯一、不含網域，驗證成功後以 SID 固定綁定。");

            migrationBuilder.AddColumn<bool>(
                name: "AdEnabled",
                schema: "identity",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true,
                comment: "是否允許使用 AD／Windows 整合驗證登入。");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                schema: "identity",
                table: "Users",
                type: "datetimeoffset",
                nullable: true,
                comment: "登入身分刪除時間；保留關聯與歷史資料。");

            migrationBuilder.AddColumn<bool>(
                name: "Enabled",
                schema: "identity",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true,
                comment: "是否啟用；停用不刪除歷史資料。");

            migrationBuilder.AddColumn<int>(
                name: "FailedLogins",
                schema: "identity",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0,
                comment: "本地登入連續失敗次數，用於暫時鎖定。");

            migrationBuilder.AddColumn<string>(
                name: "LocalAccount",
                schema: "identity",
                table: "Users",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                comment: "本地登入帳號正規化值；唯一且不區分大小寫。");

            migrationBuilder.AddColumn<bool>(
                name: "LocalEnabled",
                schema: "identity",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false,
                comment: "是否允許本地密碼登入；與 AD 驗證獨立。");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LockedUntil",
                schema: "identity",
                table: "Users",
                type: "datetimeoffset",
                nullable: true,
                comment: "本地帳號暫時鎖定的到期時間；空值表示未鎖定。");

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                schema: "identity",
                table: "Users",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true,
                comment: "本地密碼的 Argon2id PHC 雜湊，含版本、成本、隨機 salt 與衍生值；不可還原。");

            migrationBuilder.AddColumn<bool>(
                name: "ProfileManaged",
                schema: "identity",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false,
                comment: "姓名是否由管理者維護；開啟後 AD 目錄不覆寫姓名。");

            migrationBuilder.AddColumn<int>(
                name: "SecurityVersion",
                schema: "identity",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0,
                comment: "登入政策或密碼變更時遞增，立即撤銷舊工作階段。");

            migrationBuilder.AlterColumn<string>(
                name: "RoleId",
                schema: "access",
                table: "UserRoles",
                type: "nvarchar(64)",
                nullable: false,
                comment: "關聯角色的識別碼。",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "access",
                table: "UserRoles",
                type: "uniqueidentifier",
                nullable: false,
                comment: "關聯使用者的 Users 主鍵。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "Theme",
                schema: "identity",
                table: "UserPreferences",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: false,
                comment: "外觀偏好：system、light 或 dark。",
                oldClrType: typeof(string),
                oldType: "nvarchar(12)",
                oldMaxLength: 12);

            migrationBuilder.AlterColumn<int>(
                name: "SidebarWidth",
                schema: "identity",
                table: "UserPreferences",
                type: "int",
                nullable: false,
                defaultValue: 264,
                comment: "側欄寬度偏好。",
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 264);

            migrationBuilder.AlterColumn<bool>(
                name: "SaveLocalDrafts",
                schema: "identity",
                table: "UserPreferences",
                type: "bit",
                nullable: false,
                defaultValue: true,
                comment: "是否在瀏覽器按使用者保存草稿。",
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<bool>(
                name: "ReducedMotion",
                schema: "identity",
                table: "UserPreferences",
                type: "bit",
                nullable: false,
                comment: "是否減少動畫與動態效果。",
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<string>(
                name: "ReadingWidth",
                schema: "identity",
                table: "UserPreferences",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "standard",
                comment: "閱讀區寬度偏好。",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldDefaultValue: "standard");

            migrationBuilder.AlterColumn<double>(
                name: "ReadingLineHeight",
                schema: "identity",
                table: "UserPreferences",
                type: "float",
                nullable: false,
                defaultValue: 1.8,
                comment: "對話閱讀行高倍率。",
                oldClrType: typeof(double),
                oldType: "float",
                oldDefaultValue: 1.8);

            migrationBuilder.AlterColumn<int>(
                name: "ReadingFontSize",
                schema: "identity",
                table: "UserPreferences",
                type: "int",
                nullable: false,
                defaultValue: 17,
                comment: "對話文字大小，以 CSS px 的偏好值記錄。",
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 17);

            migrationBuilder.AlterColumn<bool>(
                name: "NotifyOnCompletion",
                schema: "identity",
                table: "UserPreferences",
                type: "bit",
                nullable: false,
                comment: "是否在背景分頁提醒回答完成。",
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<bool>(
                name: "EnterToSend",
                schema: "identity",
                table: "UserPreferences",
                type: "bit",
                nullable: false,
                defaultValue: true,
                comment: "是否以 Enter 送出提問；IME 組字不送出。",
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<string>(
                name: "Density",
                schema: "identity",
                table: "UserPreferences",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "comfortable",
                comment: "介面密度偏好。",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldDefaultValue: "comfortable");

            migrationBuilder.AlterColumn<string>(
                name: "DefaultReasoningEffort",
                schema: "identity",
                table: "UserPreferences",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "auto",
                comment: "偏好的推理強度。",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldDefaultValue: "auto");

            migrationBuilder.AlterColumn<string>(
                name: "DefaultModelId",
                schema: "identity",
                table: "UserPreferences",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true,
                comment: "偏好的核准模型識別碼；空值使用伺服器預設。",
                oldClrType: typeof(string),
                oldType: "nvarchar(160)",
                oldMaxLength: 160,
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "AutoFollow",
                schema: "identity",
                table: "UserPreferences",
                type: "bit",
                nullable: false,
                defaultValue: true,
                comment: "生成時是否跟隨最新回答。",
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "NexusUserId",
                schema: "identity",
                table: "UserPreferences",
                type: "uniqueidentifier",
                nullable: false,
                comment: "個人偏好對應使用者的主鍵與外鍵。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "SourceId",
                schema: "content",
                table: "SourceReferences",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                comment: "外部資料來源識別碼。",
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "Revision",
                schema: "content",
                table: "SourceReferences",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                comment: "外部來源或 repository 的固定版本識別。",
                oldClrType: typeof(string),
                oldType: "nvarchar(160)",
                oldMaxLength: 160);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ImportedAt",
                schema: "content",
                table: "SourceReferences",
                type: "datetimeoffset",
                nullable: false,
                comment: "此來源版本明確匯入的時間。",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<string>(
                name: "ExternalId",
                schema: "content",
                table: "SourceReferences",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                comment: "外部來源中的紀錄識別碼。",
                oldClrType: typeof(string),
                oldType: "nvarchar(160)",
                oldMaxLength: 160);

            migrationBuilder.AlterColumn<Guid>(
                name: "ArtifactId",
                schema: "content",
                table: "SourceReferences",
                type: "uniqueidentifier",
                nullable: false,
                comment: "關聯成果文件的識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "collaboration",
                table: "ShareRecipients",
                type: "uniqueidentifier",
                nullable: false,
                comment: "關聯使用者的 Users 主鍵。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "ShareId",
                schema: "collaboration",
                table: "ShareRecipients",
                type: "uniqueidentifier",
                nullable: false,
                comment: "關聯分享的識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                schema: "collaboration",
                table: "ShareLinks",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                comment: "介面顯示標題。",
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<Guid>(
                name: "SourceId",
                schema: "collaboration",
                table: "ShareLinks",
                type: "uniqueidentifier",
                nullable: false,
                comment: "外部資料來源識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "SnapshotJson",
                schema: "collaboration",
                table: "ShareLinks",
                type: "nvarchar(max)",
                nullable: false,
                comment: "分享時的固定內容快照；不隨後續編輯變動。",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerId",
                schema: "collaboration",
                table: "ShareLinks",
                type: "uniqueidentifier",
                nullable: false,
                comment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                schema: "collaboration",
                table: "ShareLinks",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: false,
                comment: "業務操作、資源或成本的種類。",
                oldClrType: typeof(string),
                oldType: "nvarchar(24)",
                oldMaxLength: 24);

            migrationBuilder.AlterColumn<bool>(
                name: "IsRevoked",
                schema: "collaboration",
                table: "ShareLinks",
                type: "bit",
                nullable: false,
                comment: "分享是否已撤銷。",
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<bool>(
                name: "IncludeAttachments",
                schema: "collaboration",
                table: "ShareLinks",
                type: "bit",
                nullable: false,
                comment: "是否明確允許分享附件。",
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ExpiresAt",
                schema: "collaboration",
                table: "ShareLinks",
                type: "datetimeoffset",
                nullable: false,
                comment: "分享或授權到期時間。",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "collaboration",
                table: "ShareLinks",
                type: "datetimeoffset",
                nullable: false,
                comment: "資料建立時間，採 UTC offset。",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "collaboration",
                table: "ShareLinks",
                type: "uniqueidentifier",
                nullable: false,
                comment: "資料的主鍵識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                schema: "inference",
                table: "RunEvents",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                comment: "事件的種類。",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "inference",
                table: "RunEvents",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                comment: "業務執行狀態。",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<string>(
                name: "ErrorCode",
                schema: "inference",
                table: "RunEvents",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true,
                comment: "對外安全的錯誤代碼，不含密碼或完整例外。",
                oldClrType: typeof(string),
                oldType: "nvarchar(80)",
                oldMaxLength: 80,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Delta",
                schema: "inference",
                table: "RunEvents",
                type: "nvarchar(max)",
                nullable: true,
                comment: "生成文字增量或完整快照。",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "inference",
                table: "RunEvents",
                type: "datetimeoffset",
                nullable: false,
                comment: "資料建立時間，採 UTC offset。",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<long>(
                name: "Sequence",
                schema: "inference",
                table: "RunEvents",
                type: "bigint",
                nullable: false,
                comment: "事件在同一生成中的遞增序號。",
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<Guid>(
                name: "RunId",
                schema: "inference",
                table: "RunEvents",
                type: "uniqueidentifier",
                nullable: false,
                comment: "關聯生成或評測執行的識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "access",
                table: "Roles",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                comment: "業務物件的顯示名稱。",
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<bool>(
                name: "Enabled",
                schema: "access",
                table: "Roles",
                type: "bit",
                nullable: false,
                comment: "是否啟用；停用不刪除歷史資料。",
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                schema: "access",
                table: "Roles",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                comment: "資料的主鍵識別碼。",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "access",
                table: "RoleGroups",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                comment: "業務物件的顯示名稱。",
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<bool>(
                name: "Enabled",
                schema: "access",
                table: "RoleGroups",
                type: "bit",
                nullable: false,
                comment: "是否啟用；停用不刪除歷史資料。",
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                schema: "access",
                table: "RoleGroups",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                comment: "資料的主鍵識別碼。",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "GroupId",
                schema: "access",
                table: "RoleGroupRoles",
                type: "nvarchar(64)",
                nullable: false,
                comment: "關聯功能群組的識別碼。",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)");

            migrationBuilder.AlterColumn<string>(
                name: "RoleId",
                schema: "access",
                table: "RoleGroupRoles",
                type: "nvarchar(64)",
                nullable: false,
                comment: "關聯角色的識別碼。",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)");

            migrationBuilder.AlterColumn<string>(
                name: "FeatureId",
                schema: "access",
                table: "RoleGroupFeatures",
                type: "nvarchar(64)",
                nullable: false,
                comment: "關聯功能的識別碼。",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)");

            migrationBuilder.AlterColumn<string>(
                name: "GroupId",
                schema: "access",
                table: "RoleGroupFeatures",
                type: "nvarchar(64)",
                nullable: false,
                comment: "關聯功能群組的識別碼。",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                schema: "collaboration",
                table: "Resources",
                type: "datetimeoffset",
                nullable: false,
                comment: "資料最後修改時間，採 UTC offset。",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<Guid>(
                name: "ParentId",
                schema: "collaboration",
                table: "Resources",
                type: "uniqueidentifier",
                nullable: true,
                comment: "父訊息或父資源識別碼，用於分支或階層繼承。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerId",
                schema: "collaboration",
                table: "Resources",
                type: "uniqueidentifier",
                nullable: false,
                comment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "collaboration",
                table: "Resources",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                comment: "業務物件的顯示名稱。",
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                schema: "collaboration",
                table: "Resources",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: false,
                comment: "業務操作、資源或成本的種類。",
                oldClrType: typeof(string),
                oldType: "nvarchar(24)",
                oldMaxLength: 24);

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "collaboration",
                table: "Resources",
                type: "bit",
                nullable: false,
                comment: "是否邏輯刪除；不自動刪除歷史紀錄。",
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "collaboration",
                table: "Resources",
                type: "datetimeoffset",
                nullable: false,
                comment: "資料建立時間，採 UTC offset。",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "collaboration",
                table: "Resources",
                type: "uniqueidentifier",
                nullable: false,
                comment: "資料的主鍵識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                schema: "collaboration",
                table: "ResourceMembers",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: false,
                comment: "訊息角色（user／assistant）或資源成員的閱讀／編輯權限。",
                oldClrType: typeof(string),
                oldType: "nvarchar(12)",
                oldMaxLength: 12);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "collaboration",
                table: "ResourceMembers",
                type: "uniqueidentifier",
                nullable: false,
                comment: "關聯使用者的 Users 主鍵。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "ResourceId",
                schema: "collaboration",
                table: "ResourceMembers",
                type: "uniqueidentifier",
                nullable: false,
                comment: "關聯或稽核對象的業務資源識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "GroupId",
                schema: "collaboration",
                table: "ResourceGroups",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                comment: "關聯功能群組的識別碼。",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<Guid>(
                name: "ResourceId",
                schema: "collaboration",
                table: "ResourceGroups",
                type: "uniqueidentifier",
                nullable: false,
                comment: "關聯或稽核對象的業務資源識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "AttachmentId",
                schema: "attachments",
                table: "ResourceAttachments",
                type: "uniqueidentifier",
                nullable: false,
                comment: "引用的附件識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "ResourceId",
                schema: "attachments",
                table: "ResourceAttachments",
                type: "uniqueidentifier",
                nullable: false,
                comment: "關聯或稽核對象的業務資源識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "Repository",
                schema: "knowledge",
                table: "RepositoryImports",
                type: "nvarchar(201)",
                maxLength: 201,
                nullable: false,
                comment: "Gitea repository 的 owner/name 識別。",
                oldClrType: typeof(string),
                oldType: "nvarchar(201)",
                oldMaxLength: 201);

            migrationBuilder.AlterColumn<string>(
                name: "Path",
                schema: "knowledge",
                table: "RepositoryImports",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                comment: "repository 內的檔案路徑，不是伺服器路徑。",
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerId",
                schema: "knowledge",
                table: "RepositoryImports",
                type: "uniqueidentifier",
                nullable: false,
                comment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "DocumentId",
                schema: "knowledge",
                table: "RepositoryImports",
                type: "uniqueidentifier",
                nullable: false,
                comment: "關聯知識文件的識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "Commit",
                schema: "knowledge",
                table: "RepositoryImports",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                comment: "匯入當時固定的 commit SHA。",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<Guid>(
                name: "CollectionId",
                schema: "knowledge",
                table: "RepositoryImports",
                type: "uniqueidentifier",
                nullable: false,
                comment: "關聯知識庫的識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "BaseUrl",
                schema: "knowledge",
                table: "RepositoryImports",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                comment: "Gitea 連線主機位址。",
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "knowledge",
                table: "RepositoryImports",
                type: "uniqueidentifier",
                nullable: false,
                comment: "資料的主鍵識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "ProtectedToken",
                schema: "workspace",
                table: "RepositoryConnections",
                type: "nvarchar(max)",
                maxLength: 4096,
                nullable: false,
                comment: "以 ASP.NET Data Protection 保護的外部 token；不可在 API、稽核或日誌回傳。",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldMaxLength: 4096);

            migrationBuilder.AlterColumn<string>(
                name: "Login",
                schema: "workspace",
                table: "RepositoryConnections",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                comment: "外部服務的使用者登入名稱。",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ConnectedAt",
                schema: "workspace",
                table: "RepositoryConnections",
                type: "datetimeoffset",
                nullable: false,
                comment: "使用者建立外部服務連線的時間。",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<string>(
                name: "BaseUrl",
                schema: "workspace",
                table: "RepositoryConnections",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                comment: "Gitea 連線主機位址。",
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerId",
                schema: "workspace",
                table: "RepositoryConnections",
                type: "uniqueidentifier",
                nullable: false,
                comment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                schema: "library",
                table: "PromptTemplates",
                type: "datetimeoffset",
                nullable: false,
                comment: "資料最後修改時間，採 UTC offset。",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                schema: "library",
                table: "PromptTemplates",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                comment: "介面顯示標題。",
                oldClrType: typeof(string),
                oldType: "nvarchar(80)",
                oldMaxLength: 80);

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerId",
                schema: "library",
                table: "PromptTemplates",
                type: "uniqueidentifier",
                nullable: false,
                comment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                schema: "library",
                table: "PromptTemplates",
                type: "nvarchar(max)",
                maxLength: 12000,
                nullable: false,
                comment: "訊息、版本或生成的文字內容。",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldMaxLength: 12000);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "library",
                table: "PromptTemplates",
                type: "uniqueidentifier",
                nullable: false,
                comment: "資料的主鍵識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                schema: "projects",
                table: "ProjectTemplates",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                comment: "介面顯示標題。",
                oldClrType: typeof(string),
                oldType: "nvarchar(80)",
                oldMaxLength: 80);

            migrationBuilder.AlterColumn<Guid>(
                name: "ProjectId",
                schema: "projects",
                table: "ProjectTemplates",
                type: "uniqueidentifier",
                nullable: false,
                comment: "關聯專案的識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                schema: "projects",
                table: "ProjectTemplates",
                type: "nvarchar(max)",
                maxLength: 12000,
                nullable: false,
                comment: "訊息、版本或生成的文字內容。",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldMaxLength: 12000);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "projects",
                table: "ProjectTemplates",
                type: "uniqueidentifier",
                nullable: false,
                comment: "資料的主鍵識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<int>(
                name: "Version",
                schema: "projects",
                table: "Projects",
                type: "int",
                nullable: false,
                comment: "業務版本號，用於歷史或樂觀並行控制。",
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<bool>(
                name: "IsArchived",
                schema: "projects",
                table: "Projects",
                type: "bit",
                nullable: false,
                comment: "是否封存對話；封存後不再接受新的生成。",
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<string>(
                name: "Instructions",
                schema: "projects",
                table: "Projects",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                comment: "專案共用指令。",
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "projects",
                table: "Projects",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                comment: "業務物件的用途說明。",
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "projects",
                table: "Projects",
                type: "uniqueidentifier",
                nullable: false,
                comment: "資料的主鍵識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<bool>(
                name: "SupportsUsage",
                schema: "inference",
                table: "ModelProfiles",
                type: "bit",
                nullable: false,
                comment: "模型是否會回報實際用量。",
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<bool>(
                name: "SupportsStreaming",
                schema: "inference",
                table: "ModelProfiles",
                type: "bit",
                nullable: false,
                comment: "模型是否支援串流輸出。",
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<int>(
                name: "MaxOutputTokens",
                schema: "inference",
                table: "ModelProfiles",
                type: "int",
                nullable: false,
                comment: "模型核准的最大輸出 tokens。",
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                schema: "inference",
                table: "ModelProfiles",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                comment: "使用者或模型的介面顯示名稱。",
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<int>(
                name: "ContextTokens",
                schema: "inference",
                table: "ModelProfiles",
                type: "int",
                nullable: false,
                comment: "模型上下文容量，以 tokens 計。",
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                schema: "inference",
                table: "ModelProfiles",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                comment: "資料的主鍵識別碼。",
                oldClrType: typeof(string),
                oldType: "nvarchar(160)",
                oldMaxLength: 160);

            migrationBuilder.AlterColumn<string>(
                name: "RequestCharge",
                schema: "inference",
                table: "ModelPrices",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                comment: "搜尋服務每次請求的費用。",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<string>(
                name: "Provider",
                schema: "inference",
                table: "ModelPrices",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                comment: "模型或搜尋服務供應商識別碼。",
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<decimal>(
                name: "PerRequest",
                schema: "inference",
                table: "ModelPrices",
                type: "decimal(20,8)",
                precision: 20,
                scale: 8,
                nullable: false,
                comment: "每次呼叫的固定單價。",
                oldClrType: typeof(decimal),
                oldType: "decimal(20,8)",
                oldPrecision: 20,
                oldScale: 8);

            migrationBuilder.AlterColumn<decimal>(
                name: "OutputPerMillion",
                schema: "inference",
                table: "ModelPrices",
                type: "decimal(20,8)",
                precision: 20,
                scale: 8,
                nullable: false,
                comment: "每百萬輸出 tokens 的單價。",
                oldClrType: typeof(decimal),
                oldType: "decimal(20,8)",
                oldPrecision: 20,
                oldScale: 8);

            migrationBuilder.AlterColumn<string>(
                name: "Note",
                schema: "inference",
                table: "ModelPrices",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                comment: "使用者提供的補充說明。",
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "ModelId",
                schema: "inference",
                table: "ModelPrices",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                comment: "核准模型的內部識別碼。",
                oldClrType: typeof(string),
                oldType: "nvarchar(160)",
                oldMaxLength: 160);

            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                schema: "inference",
                table: "ModelPrices",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                comment: "業務操作、資源或成本的種類。",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<decimal>(
                name: "InputPerMillion",
                schema: "inference",
                table: "ModelPrices",
                type: "decimal(20,8)",
                precision: 20,
                scale: 8,
                nullable: false,
                comment: "每百萬輸入 tokens 的單價。",
                oldClrType: typeof(decimal),
                oldType: "decimal(20,8)",
                oldPrecision: 20,
                oldScale: 8);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "EffectiveAt",
                schema: "inference",
                table: "ModelPrices",
                type: "datetimeoffset",
                nullable: false,
                comment: "此價格版本開始生效的時間。",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                schema: "inference",
                table: "ModelPrices",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                comment: "費用幣別代碼；不同幣別不可直接合計。",
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3);

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedBy",
                schema: "inference",
                table: "ModelPrices",
                type: "uniqueidentifier",
                nullable: false,
                comment: "建立紀錄的使用者識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "inference",
                table: "ModelPrices",
                type: "datetimeoffset",
                nullable: false,
                comment: "資料建立時間，採 UTC offset。",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<decimal>(
                name: "CachedInputPerMillion",
                schema: "inference",
                table: "ModelPrices",
                type: "decimal(20,8)",
                precision: 20,
                scale: 8,
                nullable: false,
                comment: "每百萬快取輸入 tokens 的單價。",
                oldClrType: typeof(decimal),
                oldType: "decimal(20,8)",
                oldPrecision: 20,
                oldScale: 8);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "inference",
                table: "ModelPrices",
                type: "uniqueidentifier",
                nullable: false,
                comment: "資料的主鍵識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "inference",
                table: "ModelInvocations",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                comment: "業務執行狀態。",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerId",
                schema: "inference",
                table: "ModelInvocations",
                type: "uniqueidentifier",
                nullable: false,
                comment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<long>(
                name: "OutputTokens",
                schema: "inference",
                table: "ModelInvocations",
                type: "bigint",
                nullable: true,
                comment: "模型回報的輸出 tokens；未知保持空值。",
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ModelId",
                schema: "inference",
                table: "ModelInvocations",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                comment: "核准模型的內部識別碼。",
                oldClrType: typeof(string),
                oldType: "nvarchar(160)",
                oldMaxLength: 160);

            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                schema: "inference",
                table: "ModelInvocations",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                comment: "業務操作、資源或成本的種類。",
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<long>(
                name: "InputTokens",
                schema: "inference",
                table: "ModelInvocations",
                type: "bigint",
                nullable: true,
                comment: "模型回報的輸入 tokens；未知保持空值。",
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "inference",
                table: "ModelInvocations",
                type: "datetimeoffset",
                nullable: false,
                comment: "資料建立時間，採 UTC offset。",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "inference",
                table: "ModelInvocations",
                type: "uniqueidentifier",
                nullable: false,
                comment: "資料的主鍵識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<bool>(
                name: "UsageComplete",
                schema: "inference",
                table: "ModelCharges",
                type: "bit",
                nullable: false,
                comment: "本次呼叫是否有完整且可計費的實際用量。",
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<string>(
                name: "State",
                schema: "inference",
                table: "ModelCharges",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: false,
                comment: "業務物件的生命週期狀態。",
                oldClrType: typeof(string),
                oldType: "nvarchar(24)",
                oldMaxLength: 24);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "StartedAt",
                schema: "inference",
                table: "ModelCharges",
                type: "datetimeoffset",
                nullable: true,
                comment: "工作開始執行時間。",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RequestCharge",
                schema: "inference",
                table: "ModelCharges",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                comment: "搜尋服務每次請求的費用。",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<long>(
                name: "ReasoningTokens",
                schema: "inference",
                table: "ModelCharges",
                type: "bigint",
                nullable: true,
                comment: "模型回報的推理 tokens。",
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Provider",
                schema: "inference",
                table: "ModelCharges",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                comment: "模型或搜尋服務供應商識別碼。",
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<Guid>(
                name: "PriceId",
                schema: "inference",
                table: "ModelCharges",
                type: "uniqueidentifier",
                nullable: true,
                comment: "此呼叫採用的不可變價格版本。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "PerRequest",
                schema: "inference",
                table: "ModelCharges",
                type: "decimal(20,8)",
                precision: 20,
                scale: 8,
                nullable: false,
                comment: "每次呼叫的固定單價。",
                oldClrType: typeof(decimal),
                oldType: "decimal(20,8)",
                oldPrecision: 20,
                oldScale: 8);

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerId",
                schema: "inference",
                table: "ModelCharges",
                type: "uniqueidentifier",
                nullable: false,
                comment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<long>(
                name: "OutputTokens",
                schema: "inference",
                table: "ModelCharges",
                type: "bigint",
                nullable: true,
                comment: "模型回報的輸出 tokens；未知保持空值。",
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "OutputPerMillion",
                schema: "inference",
                table: "ModelCharges",
                type: "decimal(20,8)",
                precision: 20,
                scale: 8,
                nullable: false,
                comment: "每百萬輸出 tokens 的單價。",
                oldClrType: typeof(decimal),
                oldType: "decimal(20,8)",
                oldPrecision: 20,
                oldScale: 8);

            migrationBuilder.AlterColumn<string>(
                name: "Outcome",
                schema: "inference",
                table: "ModelCharges",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                comment: "搜尋或處理操作的結果分類。",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<string>(
                name: "Operation",
                schema: "inference",
                table: "ModelCharges",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                comment: "模型呼叫或背景工作的操作類型。",
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "ModelId",
                schema: "inference",
                table: "ModelCharges",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                comment: "核准模型的內部識別碼。",
                oldClrType: typeof(string),
                oldType: "nvarchar(160)",
                oldMaxLength: 160);

            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                schema: "inference",
                table: "ModelCharges",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                comment: "業務操作、資源或成本的種類。",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<long>(
                name: "InputTokens",
                schema: "inference",
                table: "ModelCharges",
                type: "bigint",
                nullable: true,
                comment: "模型回報的輸入 tokens；未知保持空值。",
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "InputPerMillion",
                schema: "inference",
                table: "ModelCharges",
                type: "decimal(20,8)",
                precision: 20,
                scale: 8,
                nullable: false,
                comment: "每百萬輸入 tokens 的單價。",
                oldClrType: typeof(decimal),
                oldType: "decimal(20,8)",
                oldPrecision: 20,
                oldScale: 8);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "FinishedAt",
                schema: "inference",
                table: "ModelCharges",
                type: "datetimeoffset",
                nullable: true,
                comment: "工作結束時間。",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                schema: "inference",
                table: "ModelCharges",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                comment: "費用幣別代碼；不同幣別不可直接合計。",
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "inference",
                table: "ModelCharges",
                type: "datetimeoffset",
                nullable: false,
                comment: "資料建立時間，採 UTC offset。",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<Guid>(
                name: "ConversationId",
                schema: "inference",
                table: "ModelCharges",
                type: "uniqueidentifier",
                nullable: true,
                comment: "關聯對話的識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "CachedInputTokens",
                schema: "inference",
                table: "ModelCharges",
                type: "bigint",
                nullable: true,
                comment: "模型回報的快取輸入 tokens。",
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "CachedInputPerMillion",
                schema: "inference",
                table: "ModelCharges",
                type: "decimal(20,8)",
                precision: 20,
                scale: 8,
                nullable: false,
                comment: "每百萬快取輸入 tokens 的單價。",
                oldClrType: typeof(decimal),
                oldType: "decimal(20,8)",
                oldPrecision: 20,
                oldScale: 8);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                schema: "inference",
                table: "ModelCharges",
                type: "decimal(20,8)",
                precision: 20,
                scale: 8,
                nullable: true,
                comment: "此呼叫的已知費用；未知保持空值。",
                oldClrType: typeof(decimal),
                oldType: "decimal(20,8)",
                oldPrecision: 20,
                oldScale: 8,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "inference",
                table: "ModelCharges",
                type: "uniqueidentifier",
                nullable: false,
                comment: "資料的主鍵識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "conversations",
                table: "Messages",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                comment: "業務執行狀態。",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<Guid>(
                name: "RunId",
                schema: "conversations",
                table: "Messages",
                type: "uniqueidentifier",
                nullable: true,
                comment: "關聯生成或評測執行的識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                schema: "conversations",
                table: "Messages",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                comment: "訊息角色（user／assistant）或資源成員的閱讀／編輯權限。",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<Guid>(
                name: "ParentId",
                schema: "conversations",
                table: "Messages",
                type: "uniqueidentifier",
                nullable: true,
                comment: "父訊息或父資源識別碼，用於分支或階層繼承。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ModelId",
                schema: "conversations",
                table: "Messages",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true,
                comment: "核准模型的內部識別碼。",
                oldClrType: typeof(string),
                oldType: "nvarchar(160)",
                oldMaxLength: 160,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ErrorCode",
                schema: "conversations",
                table: "Messages",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true,
                comment: "對外安全的錯誤代碼，不含密碼或完整例外。",
                oldClrType: typeof(string),
                oldType: "nvarchar(80)",
                oldMaxLength: 80,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "conversations",
                table: "Messages",
                type: "datetimeoffset",
                nullable: false,
                comment: "資料建立時間，採 UTC offset。",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<Guid>(
                name: "ConversationId",
                schema: "conversations",
                table: "Messages",
                type: "uniqueidentifier",
                nullable: false,
                comment: "關聯對話的識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                schema: "conversations",
                table: "Messages",
                type: "nvarchar(max)",
                nullable: false,
                comment: "訊息、版本或生成的文字內容。",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "conversations",
                table: "Messages",
                type: "uniqueidentifier",
                nullable: false,
                comment: "資料的主鍵識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                schema: "quality",
                table: "MessageFeedback",
                type: "datetimeoffset",
                nullable: false,
                comment: "資料最後修改時間，採 UTC offset。",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                schema: "quality",
                table: "MessageFeedback",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: false,
                comment: "回饋或操作理由。",
                oldClrType: typeof(string),
                oldType: "nvarchar(24)",
                oldMaxLength: 24);

            migrationBuilder.AlterColumn<int>(
                name: "Rating",
                schema: "quality",
                table: "MessageFeedback",
                type: "int",
                nullable: false,
                comment: "使用者對回答的評分。",
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerId",
                schema: "quality",
                table: "MessageFeedback",
                type: "uniqueidentifier",
                nullable: false,
                comment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "Note",
                schema: "quality",
                table: "MessageFeedback",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                comment: "使用者提供的補充說明。",
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<Guid>(
                name: "MessageId",
                schema: "quality",
                table: "MessageFeedback",
                type: "uniqueidentifier",
                nullable: false,
                comment: "關聯訊息的識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                schema: "knowledge",
                table: "MessageCitations",
                type: "nvarchar(180)",
                maxLength: 180,
                nullable: false,
                comment: "介面顯示標題。",
                oldClrType: typeof(string),
                oldType: "nvarchar(180)",
                oldMaxLength: 180);

            migrationBuilder.AlterColumn<int>(
                name: "PageNumber",
                schema: "knowledge",
                table: "MessageCitations",
                type: "int",
                nullable: false,
                comment: "文件頁碼，從 1 開始。",
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "Excerpt",
                schema: "knowledge",
                table: "MessageCitations",
                type: "nvarchar(800)",
                maxLength: 800,
                nullable: false,
                comment: "檢索或引用時保存的文字摘要。",
                oldClrType: typeof(string),
                oldType: "nvarchar(800)",
                oldMaxLength: 800);

            migrationBuilder.AlterColumn<Guid>(
                name: "DocumentId",
                schema: "knowledge",
                table: "MessageCitations",
                type: "uniqueidentifier",
                nullable: false,
                comment: "關聯知識文件的識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<int>(
                name: "Number",
                schema: "knowledge",
                table: "MessageCitations",
                type: "int",
                nullable: false,
                comment: "回答引用的順序編號。",
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<Guid>(
                name: "MessageId",
                schema: "knowledge",
                table: "MessageCitations",
                type: "uniqueidentifier",
                nullable: false,
                comment: "關聯訊息的識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "AttachmentId",
                schema: "attachments",
                table: "MessageAttachments",
                type: "uniqueidentifier",
                nullable: false,
                comment: "引用的附件識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "MessageId",
                schema: "attachments",
                table: "MessageAttachments",
                type: "uniqueidentifier",
                nullable: false,
                comment: "關聯訊息的識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<long>(
                name: "StoredAttachmentLimitBytes",
                schema: "access",
                table: "GroupModelPolicies",
                type: "bigint",
                nullable: true,
                comment: "個人附件儲存上限，以 bytes 計；群組限制取最低值。",
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "DailyRequestLimit",
                schema: "access",
                table: "GroupModelPolicies",
                type: "int",
                nullable: true,
                comment: "每日生成次數上限；群組限制取最低值，UTC 午夜重設。",
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AllowedModelsJson",
                schema: "access",
                table: "GroupModelPolicies",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true,
                comment: "模型白名單 JSON；空值不增加限制，空陣列禁止生成。",
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "GroupId",
                schema: "access",
                table: "GroupModelPolicies",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                comment: "關聯功能群組的識別碼。",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserMessageId",
                schema: "inference",
                table: "GenerationRuns",
                type: "uniqueidentifier",
                nullable: false,
                comment: "此次生成的使用者提問訊息識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "inference",
                table: "GenerationRuns",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                comment: "業務執行狀態。",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "StartedAt",
                schema: "inference",
                table: "GenerationRuns",
                type: "datetimeoffset",
                nullable: true,
                comment: "工作開始執行時間。",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RequestHash",
                schema: "inference",
                table: "GenerationRuns",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                comment: "請求內容指紋，用於辨識冪等識別碼衝突。",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "ParametersJson",
                schema: "inference",
                table: "GenerationRuns",
                type: "nvarchar(max)",
                nullable: false,
                comment: "執行參數的 JSON 快照，不含服務密鑰。",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerId",
                schema: "inference",
                table: "GenerationRuns",
                type: "uniqueidentifier",
                nullable: false,
                comment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<long>(
                name: "OutputTokens",
                schema: "inference",
                table: "GenerationRuns",
                type: "bigint",
                nullable: true,
                comment: "模型回報的輸出 tokens；未知保持空值。",
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ModelId",
                schema: "inference",
                table: "GenerationRuns",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                comment: "核准模型的內部識別碼。",
                oldClrType: typeof(string),
                oldType: "nvarchar(160)",
                oldMaxLength: 160);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "LeaseExpiresAt",
                schema: "inference",
                table: "GenerationRuns",
                type: "datetimeoffset",
                nullable: true,
                comment: "生成 executor 租約的到期時間。",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "LastSequence",
                schema: "inference",
                table: "GenerationRuns",
                type: "bigint",
                nullable: false,
                comment: "最後已持久化的生成事件序號。",
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<long>(
                name: "InputTokens",
                schema: "inference",
                table: "GenerationRuns",
                type: "bigint",
                nullable: true,
                comment: "模型回報的輸入 tokens；未知保持空值。",
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "IdempotencyKey",
                schema: "inference",
                table: "GenerationRuns",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                comment: "擁有者範圍內的冪等請求識別，避免重試重複處理。",
                oldClrType: typeof(string),
                oldType: "nvarchar(80)",
                oldMaxLength: 80);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "FinishedAt",
                schema: "inference",
                table: "GenerationRuns",
                type: "datetimeoffset",
                nullable: true,
                comment: "工作結束時間。",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ExecutorId",
                schema: "inference",
                table: "GenerationRuns",
                type: "uniqueidentifier",
                nullable: true,
                comment: "處理此次生成的伺服器程序識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ErrorCode",
                schema: "inference",
                table: "GenerationRuns",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true,
                comment: "對外安全的錯誤代碼，不含密碼或完整例外。",
                oldClrType: typeof(string),
                oldType: "nvarchar(80)",
                oldMaxLength: 80,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "inference",
                table: "GenerationRuns",
                type: "datetimeoffset",
                nullable: false,
                comment: "資料建立時間，採 UTC offset。",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<Guid>(
                name: "ConversationId",
                schema: "inference",
                table: "GenerationRuns",
                type: "uniqueidentifier",
                nullable: false,
                comment: "關聯對話的識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                schema: "inference",
                table: "GenerationRuns",
                type: "nvarchar(max)",
                nullable: false,
                comment: "訊息、版本或生成的文字內容。",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<Guid>(
                name: "AssistantMessageId",
                schema: "inference",
                table: "GenerationRuns",
                type: "uniqueidentifier",
                nullable: false,
                comment: "此次生成的 AI 回答訊息識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "ActiveOwnerId",
                schema: "inference",
                table: "GenerationRuns",
                type: "uniqueidentifier",
                nullable: true,
                comment: "仍在執行的擁有者；filtered unique index 限制每人一個生成。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "inference",
                table: "GenerationRuns",
                type: "uniqueidentifier",
                nullable: false,
                comment: "資料的主鍵識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<int>(
                name: "SortOrder",
                schema: "access",
                table: "Features",
                type: "int",
                nullable: false,
                comment: "介面顯示順序；數值越小越前。",
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "Route",
                schema: "access",
                table: "Features",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                comment: "功能入口的本站路由；API 授權仍由後端政策判定。",
                oldClrType: typeof(string),
                oldType: "nvarchar(160)",
                oldMaxLength: 160);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "access",
                table: "Features",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                comment: "業務物件的顯示名稱。",
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<bool>(
                name: "Enabled",
                schema: "access",
                table: "Features",
                type: "bit",
                nullable: false,
                comment: "是否啟用；停用不刪除歷史資料。",
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                schema: "access",
                table: "Features",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                comment: "資料的主鍵識別碼。",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<int>(
                name: "Version",
                schema: "quality",
                table: "EvaluationSets",
                type: "int",
                nullable: false,
                comment: "業務版本號，用於歷史或樂觀並行控制。",
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "quality",
                table: "EvaluationSets",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                comment: "業務物件的用途說明。",
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<string>(
                name: "CasesJson",
                schema: "quality",
                table: "EvaluationSets",
                type: "nvarchar(max)",
                nullable: false,
                comment: "固定評測案例的 JSON 快照。",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "quality",
                table: "EvaluationSets",
                type: "uniqueidentifier",
                nullable: false,
                comment: "資料的主鍵識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "VariantsJson",
                schema: "quality",
                table: "EvaluationRuns",
                type: "nvarchar(max)",
                nullable: false,
                comment: "評測模型／參數組合的 JSON 快照。",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<int>(
                name: "SetVersion",
                schema: "quality",
                table: "EvaluationRuns",
                type: "int",
                nullable: false,
                comment: "評測執行當時的題庫版本。",
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "SetTitle",
                schema: "quality",
                table: "EvaluationRuns",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                comment: "評測執行當時的題庫名稱快照。",
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<Guid>(
                name: "SetId",
                schema: "quality",
                table: "EvaluationRuns",
                type: "uniqueidentifier",
                nullable: false,
                comment: "評測題庫識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerId",
                schema: "quality",
                table: "EvaluationRuns",
                type: "uniqueidentifier",
                nullable: false,
                comment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "JobId",
                schema: "quality",
                table: "EvaluationRuns",
                type: "uniqueidentifier",
                nullable: false,
                comment: "關聯背景工作識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "quality",
                table: "EvaluationRuns",
                type: "datetimeoffset",
                nullable: false,
                comment: "資料建立時間，採 UTC offset。",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<string>(
                name: "CasesJson",
                schema: "quality",
                table: "EvaluationRuns",
                type: "nvarchar(max)",
                nullable: false,
                comment: "固定評測案例的 JSON 快照。",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "quality",
                table: "EvaluationRuns",
                type: "uniqueidentifier",
                nullable: false,
                comment: "資料的主鍵識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<bool>(
                name: "Truncated",
                schema: "quality",
                table: "EvaluationResults",
                type: "bit",
                nullable: false,
                comment: "評測輸出是否因上限截斷。",
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<Guid>(
                name: "ReviewerId",
                schema: "quality",
                table: "EvaluationResults",
                type: "uniqueidentifier",
                nullable: true,
                comment: "人工覆核者的使用者識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ReviewScore",
                schema: "quality",
                table: "EvaluationResults",
                type: "int",
                nullable: true,
                comment: "人工覆核分數；未覆核保持空值。",
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ReviewNote",
                schema: "quality",
                table: "EvaluationResults",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                comment: "人工覆核意見。",
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<int>(
                name: "RequiredTotal",
                schema: "quality",
                table: "EvaluationResults",
                type: "int",
                nullable: false,
                comment: "必要條件總數。",
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "RequiredMatches",
                schema: "quality",
                table: "EvaluationResults",
                type: "int",
                nullable: false,
                comment: "命中的必要條件數。",
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<long>(
                name: "OutputTokens",
                schema: "quality",
                table: "EvaluationResults",
                type: "bigint",
                nullable: true,
                comment: "模型回報的輸出 tokens；未知保持空值。",
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Output",
                schema: "quality",
                table: "EvaluationResults",
                type: "nvarchar(max)",
                nullable: false,
                comment: "評測模型的實際回答。",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<long>(
                name: "InputTokens",
                schema: "quality",
                table: "EvaluationResults",
                type: "bigint",
                nullable: true,
                comment: "模型回報的輸入 tokens；未知保持空值。",
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ForbiddenMatches",
                schema: "quality",
                table: "EvaluationResults",
                type: "int",
                nullable: false,
                comment: "命中的禁止條件數。",
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<long>(
                name: "ElapsedMs",
                schema: "quality",
                table: "EvaluationResults",
                type: "bigint",
                nullable: false,
                comment: "執行耗時，以毫秒計。",
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "VariantIndex",
                schema: "quality",
                table: "EvaluationResults",
                type: "int",
                nullable: false,
                comment: "評測模型／參數組合的從零開始索引。",
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "CaseIndex",
                schema: "quality",
                table: "EvaluationResults",
                type: "int",
                nullable: false,
                comment: "評測案例的從零開始索引。",
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<Guid>(
                name: "RunId",
                schema: "quality",
                table: "EvaluationResults",
                type: "uniqueidentifier",
                nullable: false,
                comment: "關聯生成或評測執行的識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "Warning",
                schema: "knowledge",
                table: "Documents",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                comment: "處理過程中的非致命提示。",
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "knowledge",
                table: "Documents",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                comment: "業務執行狀態。",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<int>(
                name: "PageCount",
                schema: "knowledge",
                table: "Documents",
                type: "int",
                nullable: false,
                comment: "文件總頁數。",
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<Guid>(
                name: "JobId",
                schema: "knowledge",
                table: "Documents",
                type: "uniqueidentifier",
                nullable: true,
                comment: "關聯背景工作識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "knowledge",
                table: "Documents",
                type: "bit",
                nullable: false,
                comment: "是否邏輯刪除；不自動刪除歷史紀錄。",
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                schema: "knowledge",
                table: "Documents",
                type: "nvarchar(180)",
                maxLength: 180,
                nullable: false,
                comment: "原始附件檔名，不作為伺服器儲存路徑。",
                oldClrType: typeof(string),
                oldType: "nvarchar(180)",
                oldMaxLength: 180);

            migrationBuilder.AlterColumn<string>(
                name: "EmbeddingProfile",
                schema: "knowledge",
                table: "Documents",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                comment: "向量模型、維度與前處理的版本指紋；不混用不同 profile。",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ContentType",
                schema: "knowledge",
                table: "Documents",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                comment: "核准的 MIME 型別。",
                oldClrType: typeof(string),
                oldType: "nvarchar(80)",
                oldMaxLength: 80);

            migrationBuilder.AlterColumn<Guid>(
                name: "CollectionId",
                schema: "knowledge",
                table: "Documents",
                type: "uniqueidentifier",
                nullable: true,
                comment: "關聯知識庫的識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ChunkCount",
                schema: "knowledge",
                table: "Documents",
                type: "int",
                nullable: false,
                comment: "文件已建立的檢索片段數。",
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<Guid>(
                name: "AttachmentId",
                schema: "knowledge",
                table: "Documents",
                type: "uniqueidentifier",
                nullable: true,
                comment: "引用的附件識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "knowledge",
                table: "Documents",
                type: "uniqueidentifier",
                nullable: false,
                comment: "資料的主鍵識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "Text",
                schema: "knowledge",
                table: "DocumentPages",
                type: "nvarchar(max)",
                nullable: false,
                comment: "文件頁面／片段的擷取文字。",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<bool>(
                name: "NeedsReview",
                schema: "knowledge",
                table: "DocumentPages",
                type: "bit",
                nullable: false,
                comment: "此評測結果是否需要人工覆核。",
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<string>(
                name: "Extraction",
                schema: "knowledge",
                table: "DocumentPages",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                comment: "附件文字擷取方法或結果。",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<int>(
                name: "PageNumber",
                schema: "knowledge",
                table: "DocumentPages",
                type: "int",
                nullable: false,
                comment: "文件頁碼，從 1 開始。",
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<Guid>(
                name: "DocumentId",
                schema: "knowledge",
                table: "DocumentPages",
                type: "uniqueidentifier",
                nullable: false,
                comment: "關聯知識文件的識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                schema: "conversations",
                table: "Conversations",
                type: "datetimeoffset",
                nullable: false,
                comment: "資料最後修改時間，採 UTC offset。",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                schema: "conversations",
                table: "Conversations",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                comment: "介面顯示標題。",
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<string>(
                name: "SystemInstruction",
                schema: "conversations",
                table: "Conversations",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                comment: "對話專用的回答指令。",
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000);

            migrationBuilder.AlterColumn<Guid>(
                name: "ProjectId",
                schema: "conversations",
                table: "Conversations",
                type: "uniqueidentifier",
                nullable: true,
                comment: "關聯專案的識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerId",
                schema: "conversations",
                table: "Conversations",
                type: "uniqueidentifier",
                nullable: false,
                comment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<bool>(
                name: "IsFavorite",
                schema: "conversations",
                table: "Conversations",
                type: "bit",
                nullable: false,
                comment: "是否標記收藏。",
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "conversations",
                table: "Conversations",
                type: "bit",
                nullable: false,
                comment: "是否邏輯刪除；不自動刪除歷史紀錄。",
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<bool>(
                name: "IsArchived",
                schema: "conversations",
                table: "Conversations",
                type: "bit",
                nullable: false,
                comment: "是否封存對話；封存後不再接受新的生成。",
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "conversations",
                table: "Conversations",
                type: "datetimeoffset",
                nullable: false,
                comment: "資料建立時間，採 UTC offset。",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<Guid>(
                name: "ActiveLeafId",
                schema: "conversations",
                table: "Conversations",
                type: "uniqueidentifier",
                nullable: true,
                comment: "對話目前顯示分支的最後訊息識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "conversations",
                table: "Conversations",
                type: "uniqueidentifier",
                nullable: false,
                comment: "資料的主鍵識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "conversations",
                table: "ConversationLabels",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: false,
                comment: "業務物件的顯示名稱。",
                oldClrType: typeof(string),
                oldType: "nvarchar(24)",
                oldMaxLength: 24);

            migrationBuilder.AlterColumn<Guid>(
                name: "ConversationId",
                schema: "conversations",
                table: "ConversationLabels",
                type: "uniqueidentifier",
                nullable: false,
                comment: "關聯對話的識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "CollectionId",
                schema: "knowledge",
                table: "ConversationCollections",
                type: "uniqueidentifier",
                nullable: false,
                comment: "關聯知識庫的識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "ConversationId",
                schema: "knowledge",
                table: "ConversationCollections",
                type: "uniqueidentifier",
                nullable: false,
                comment: "關聯對話的識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "knowledge",
                table: "Collections",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                comment: "業務物件的用途說明。",
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "knowledge",
                table: "Collections",
                type: "uniqueidentifier",
                nullable: false,
                comment: "資料的主鍵識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "Text",
                schema: "knowledge",
                table: "Chunks",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                comment: "文件頁面／片段的擷取文字。",
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<int>(
                name: "PageNumber",
                schema: "knowledge",
                table: "Chunks",
                type: "int",
                nullable: false,
                comment: "文件頁碼，從 1 開始。",
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "Ordinal",
                schema: "knowledge",
                table: "Chunks",
                type: "int",
                nullable: false,
                comment: "同一父物件內的呈現順序。",
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "EmbeddingProfile",
                schema: "knowledge",
                table: "Chunks",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                comment: "向量模型、維度與前處理的版本指紋；不混用不同 profile。",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EmbeddingJson",
                schema: "knowledge",
                table: "Chunks",
                type: "nvarchar(max)",
                nullable: true,
                comment: "正規化向量的 JSON 表示；與 embedding profile 一起判斷相容性。",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "DocumentId",
                schema: "knowledge",
                table: "Chunks",
                type: "uniqueidentifier",
                nullable: false,
                comment: "關聯知識文件的識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "knowledge",
                table: "Chunks",
                type: "uniqueidentifier",
                nullable: false,
                comment: "資料的主鍵識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                schema: "operations",
                table: "BackgroundJobs",
                type: "datetimeoffset",
                nullable: false,
                comment: "資料最後修改時間，採 UTC offset。",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<int>(
                name: "TotalUnits",
                schema: "operations",
                table: "BackgroundJobs",
                type: "int",
                nullable: true,
                comment: "已知的總工作單位數；未知不表示百分比。",
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "SubjectId",
                schema: "operations",
                table: "BackgroundJobs",
                type: "uniqueidentifier",
                nullable: false,
                comment: "操作所關聯的業務對象識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "operations",
                table: "BackgroundJobs",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                comment: "業務執行狀態。",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<string>(
                name: "Stage",
                schema: "operations",
                table: "BackgroundJobs",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                comment: "背景工作目前階段。",
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<Guid>(
                name: "ResourceId",
                schema: "operations",
                table: "BackgroundJobs",
                type: "uniqueidentifier",
                nullable: true,
                comment: "關聯或稽核對象的業務資源識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerId",
                schema: "operations",
                table: "BackgroundJobs",
                type: "uniqueidentifier",
                nullable: false,
                comment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "LeaseUntil",
                schema: "operations",
                table: "BackgroundJobs",
                type: "datetimeoffset",
                nullable: true,
                comment: "背景工作租約的到期時間。",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "LeaseToken",
                schema: "operations",
                table: "BackgroundJobs",
                type: "uniqueidentifier",
                nullable: true,
                comment: "背景工作租約的 fencing token，防止過期 worker 提交。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Label",
                schema: "operations",
                table: "BackgroundJobs",
                type: "nvarchar(180)",
                maxLength: 180,
                nullable: false,
                comment: "分類標籤或階段的顯示文字。",
                oldClrType: typeof(string),
                oldType: "nvarchar(180)",
                oldMaxLength: 180);

            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                schema: "operations",
                table: "BackgroundJobs",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                comment: "業務操作、資源或成本的種類。",
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "ErrorMessage",
                schema: "operations",
                table: "BackgroundJobs",
                type: "nvarchar(240)",
                maxLength: 240,
                nullable: true,
                comment: "經限制的錯誤說明。",
                oldClrType: typeof(string),
                oldType: "nvarchar(240)",
                oldMaxLength: 240,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ErrorCode",
                schema: "operations",
                table: "BackgroundJobs",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true,
                comment: "對外安全的錯誤代碼，不含密碼或完整例外。",
                oldClrType: typeof(string),
                oldType: "nvarchar(80)",
                oldMaxLength: 80,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "operations",
                table: "BackgroundJobs",
                type: "datetimeoffset",
                nullable: false,
                comment: "資料建立時間，採 UTC offset。",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<int>(
                name: "CompletedUnits",
                schema: "operations",
                table: "BackgroundJobs",
                type: "int",
                nullable: false,
                comment: "已完成的真實工作單位數。",
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<bool>(
                name: "CancelRequested",
                schema: "operations",
                table: "BackgroundJobs",
                type: "bit",
                nullable: false,
                comment: "是否收到取消要求；不表示工作已停止。",
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<int>(
                name: "Attempt",
                schema: "operations",
                table: "BackgroundJobs",
                type: "int",
                nullable: false,
                comment: "背景工作執行／重試次數。",
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "ActiveKey",
                schema: "operations",
                table: "BackgroundJobs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                comment: "仍在執行工作的唯一鍵，避免同一業務重複排程。",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "operations",
                table: "BackgroundJobs",
                type: "uniqueidentifier",
                nullable: false,
                comment: "資料的主鍵識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "Result",
                schema: "operations",
                table: "AuditEvents",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true,
                comment: "稽核操作結果或失敗代碼。",
                oldClrType: typeof(string),
                oldType: "nvarchar(80)",
                oldMaxLength: 80,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ResourceId",
                schema: "operations",
                table: "AuditEvents",
                type: "uniqueidentifier",
                nullable: true,
                comment: "關聯或稽核對象的業務資源識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerId",
                schema: "operations",
                table: "AuditEvents",
                type: "uniqueidentifier",
                nullable: false,
                comment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "DetailsJson",
                schema: "operations",
                table: "AuditEvents",
                type: "nvarchar(max)",
                maxLength: 40000,
                nullable: true,
                comment: "稽核前後狀態或操作範圍 JSON；不含密碼、hash、token 或對話內容。",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldMaxLength: 40000,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "At",
                schema: "operations",
                table: "AuditEvents",
                type: "datetimeoffset",
                nullable: false,
                comment: "稽核事件發生時間。",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                schema: "operations",
                table: "AuditEvents",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                comment: "稽核操作名稱。",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<long>(
                name: "Id",
                schema: "operations",
                table: "AuditEvents",
                type: "bigint",
                nullable: false,
                comment: "資料的主鍵識別碼。",
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddColumn<Guid>(
                name: "ActorId",
                schema: "operations",
                table: "AuditEvents",
                type: "uniqueidentifier",
                nullable: true,
                comment: "身分測試時實際發起操作的管理者；空值表示與 OwnerId 相同。");

            migrationBuilder.AlterColumn<long>(
                name: "Size",
                schema: "attachments",
                table: "Attachments",
                type: "bigint",
                nullable: false,
                comment: "原始附件大小，以 bytes 計。",
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerId",
                schema: "attachments",
                table: "Attachments",
                type: "uniqueidentifier",
                nullable: false,
                comment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                schema: "attachments",
                table: "Attachments",
                type: "nvarchar(180)",
                maxLength: 180,
                nullable: false,
                comment: "原始附件檔名，不作為伺服器儲存路徑。",
                oldClrType: typeof(string),
                oldType: "nvarchar(180)",
                oldMaxLength: 180);

            migrationBuilder.AlterColumn<string>(
                name: "ExtractedText",
                schema: "attachments",
                table: "Attachments",
                type: "nvarchar(max)",
                nullable: true,
                comment: "附件分析後的文字。",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "Data",
                schema: "attachments",
                table: "Attachments",
                type: "varbinary(max)",
                nullable: false,
                comment: "原始附件二進位內容；不在 wwwroot 公開。",
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "attachments",
                table: "Attachments",
                type: "datetimeoffset",
                nullable: false,
                comment: "資料建立時間，採 UTC offset。",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<string>(
                name: "ContentType",
                schema: "attachments",
                table: "Attachments",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                comment: "核准的 MIME 型別。",
                oldClrType: typeof(string),
                oldType: "nvarchar(80)",
                oldMaxLength: 80);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "attachments",
                table: "Attachments",
                type: "uniqueidentifier",
                nullable: false,
                comment: "資料的主鍵識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<int>(
                name: "Version",
                schema: "content",
                table: "Artifacts",
                type: "int",
                nullable: false,
                comment: "業務版本號，用於歷史或樂觀並行控制。",
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<Guid>(
                name: "SourceMessageId",
                schema: "content",
                table: "Artifacts",
                type: "uniqueidentifier",
                nullable: true,
                comment: "此成果版本所引用的來源訊息識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ProjectId",
                schema: "content",
                table: "Artifacts",
                type: "uniqueidentifier",
                nullable: true,
                comment: "關聯專案的識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "content",
                table: "Artifacts",
                type: "uniqueidentifier",
                nullable: false,
                comment: "資料的主鍵識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                schema: "content",
                table: "ArtifactRevisions",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                comment: "介面顯示標題。",
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "content",
                table: "ArtifactRevisions",
                type: "datetimeoffset",
                nullable: false,
                comment: "資料建立時間，採 UTC offset。",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                schema: "content",
                table: "ArtifactRevisions",
                type: "nvarchar(max)",
                maxLength: 64000,
                nullable: false,
                comment: "訊息、版本或生成的文字內容。",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldMaxLength: 64000);

            migrationBuilder.AlterColumn<Guid>(
                name: "AuthorId",
                schema: "content",
                table: "ArtifactRevisions",
                type: "uniqueidentifier",
                nullable: false,
                comment: "建立此版本的使用者識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<int>(
                name: "Version",
                schema: "content",
                table: "ArtifactRevisions",
                type: "int",
                nullable: false,
                comment: "業務版本號，用於歷史或樂觀並行控制。",
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<Guid>(
                name: "ArtifactId",
                schema: "content",
                table: "ArtifactRevisions",
                type: "uniqueidentifier",
                nullable: false,
                comment: "關聯成果文件的識別碼。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "GrantedAt",
                schema: "access",
                table: "AdministratorBootstraps",
                type: "datetimeoffset",
                nullable: false,
                comment: "角色或資源授權建立時間。",
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "access",
                table: "AdministratorBootstraps",
                type: "uniqueidentifier",
                nullable: false,
                comment: "關聯使用者的 Users 主鍵。",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            // Preserve names already customized by an administrator.
            migrationBuilder.Sql("UPDATE [access].[RoleGroups] SET [Name] = N'基本工作區' WHERE [Id] = N'workspace' AND [Name] = N'基本工作\u53f0'");

            migrationBuilder.CreateIndex(
                name: "IX_Users_AdAccount",
                schema: "identity",
                table: "Users",
                column: "AdAccount",
                unique: true,
                filter: "[AdAccount] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_LocalAccount",
                schema: "identity",
                table: "Users",
                column: "LocalAccount",
                unique: true,
                filter: "[LocalAccount] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_ActorId_Id",
                schema: "operations",
                table: "AuditEvents",
                columns: new[] { "ActorId", "Id" });
            migrationBuilder.Sql(
"""
-- Re-runnable descriptions for schema, physical indexes/constraints and non-EF columns.
-- Table/column MS_Description is maintained by the EF model and its migrations.
SET NOCOUNT ON;
DECLARE @nxDescschema sysname, @nxDesctable sysname, @nxDescname sysname, @nxDesckind varchar(20), @nxDescdescription nvarchar(3750);
DECLARE schema_descriptions CURSOR LOCAL FAST_FORWARD FOR
SELECT name, description FROM (VALUES
 (N'identity', N'使用者身分、登入政策、密碼雜湊與個人偏好。'),
 (N'access', N'角色、功能群組、功能授權及模型政策。'),
 (N'conversations', N'私人對話、訊息分支與分類。'),
 (N'inference', N'模型設定、生成、租約、重播、用量與費用。'),
 (N'operations', N'稽核、背景工作及持久進度。'),
 (N'attachments', N'原始附件與引用關聯。'),
 (N'library', N'使用者私人提示詞範本。'),
 (N'collaboration', N'資料資源 ACL 與具名分享。'),
 (N'knowledge', N'知識文件、分頁、片段、向量與引用。'),
 (N'content', N'成果文件版本及外部來源參照。'),
 (N'projects', N'專案與共用指令範本。'),
 (N'quality', N'回答回饋、固定評測及人工覆核。'),
 (N'workspace', N'個人程式庫連線與匯入識別。'),
 (N'dbo', N'EF 資料庫版本記錄。')
) descriptions(name, description) WHERE SCHEMA_ID(name) IS NOT NULL;
OPEN schema_descriptions;
FETCH NEXT FROM schema_descriptions INTO @nxDescschema, @nxDescdescription;
WHILE @@FETCH_STATUS = 0
BEGIN
 IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE class = 3 AND major_id = SCHEMA_ID(@nxDescschema) AND name = N'MS_Description')
  EXEC sys.sp_updateextendedproperty @name=N'MS_Description', @value=@nxDescdescription, @level0type=N'SCHEMA', @level0name=@nxDescschema;
 ELSE EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=@nxDescdescription, @level0type=N'SCHEMA', @level0name=@nxDescschema;
 FETCH NEXT FROM schema_descriptions INTO @nxDescschema, @nxDescdescription;
END;
CLOSE schema_descriptions; DEALLOCATE schema_descriptions;

DECLARE object_descriptions CURSOR LOCAL FAST_FORWARD FOR
SELECT s.name, t.name, 'INDEX', i.name,
 LEFT(CONCAT(CASE WHEN i.is_unique = 1 THEN N'唯一索引；避免重複組合：' ELSE N'查詢索引；加速依下列欄位篩選及排序：' END,
  STRING_AGG(CONVERT(nvarchar(max), c.name + CASE WHEN ic.is_descending_key = 1 THEN N' DESC' ELSE N'' END), N'、') WITHIN GROUP (ORDER BY ic.key_ordinal),
  CASE WHEN i.has_filter = 1 THEN N'；篩選條件：' + i.filter_definition ELSE N'' END), 3750) COLLATE DATABASE_DEFAULT
FROM sys.indexes i JOIN sys.tables t ON i.object_id=t.object_id JOIN sys.schemas s ON t.schema_id=s.schema_id
JOIN sys.index_columns ic ON i.object_id=ic.object_id AND i.index_id=ic.index_id AND ic.key_ordinal > 0
JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
WHERE s.name IN (N'identity',N'access',N'conversations',N'inference',N'operations',N'attachments',N'library',N'collaboration',N'knowledge',N'content',N'projects',N'quality',N'workspace',N'dbo')
 AND i.is_primary_key=0 AND i.is_unique_constraint=0
GROUP BY s.name,t.name,i.name,i.is_unique,i.has_filter,i.filter_definition
UNION ALL
SELECT s.name,t.name,'CONSTRAINT',k.name,CONCAT(CASE WHEN k.type='PK' THEN N'主鍵；唯一識別 ' ELSE N'唯一約束；防止重複 ' END,s.name,N'.',t.name,N' 的資料。') COLLATE DATABASE_DEFAULT
FROM sys.key_constraints k JOIN sys.tables t ON k.parent_object_id=t.object_id JOIN sys.schemas s ON t.schema_id=s.schema_id
WHERE s.name IN (N'identity',N'access',N'conversations',N'inference',N'operations',N'attachments',N'library',N'collaboration',N'knowledge',N'content',N'projects',N'quality',N'workspace',N'dbo')
UNION ALL
SELECT s.name,t.name,'CONSTRAINT',f.name,LEFT(CONCAT(N'外鍵；關聯 ',OBJECT_SCHEMA_NAME(f.referenced_object_id),N'.',OBJECT_NAME(f.referenced_object_id),N'；刪除策略：',f.delete_referential_action_desc,N'；更新策略：',f.update_referential_action_desc),3750) COLLATE DATABASE_DEFAULT
FROM sys.foreign_keys f JOIN sys.tables t ON f.parent_object_id=t.object_id JOIN sys.schemas s ON t.schema_id=s.schema_id
WHERE s.name IN (N'identity',N'access',N'conversations',N'inference',N'operations',N'attachments',N'library',N'collaboration',N'knowledge',N'content',N'projects',N'quality',N'workspace')
UNION ALL
SELECT s.name,t.name,'CONSTRAINT',d.name,LEFT(CONCAT(N'預設約束；欄位 ',COL_NAME(d.parent_object_id,d.parent_column_id),N' 未指定值時使用 ',d.definition),3750) COLLATE DATABASE_DEFAULT
FROM sys.default_constraints d JOIN sys.tables t ON d.parent_object_id=t.object_id JOIN sys.schemas s ON t.schema_id=s.schema_id
WHERE s.name IN (N'identity',N'access',N'conversations',N'inference',N'operations',N'attachments',N'library',N'collaboration',N'knowledge',N'content',N'projects',N'quality',N'workspace')
UNION ALL
SELECT s.name,t.name,'CONSTRAINT',c.name,LEFT(CONCAT(N'檢核約束；資料需符合 ',c.definition),3750) COLLATE DATABASE_DEFAULT
FROM sys.check_constraints c JOIN sys.tables t ON c.parent_object_id=t.object_id JOIN sys.schemas s ON t.schema_id=s.schema_id
WHERE s.name IN (N'identity',N'access',N'conversations',N'inference',N'operations',N'attachments',N'library',N'collaboration',N'knowledge',N'content',N'projects',N'quality',N'workspace')
UNION ALL
SELECT s.name,t.name,'COLUMN',c.name,
 CASE WHEN c.name LIKE N'%1024%' THEN N'原生 VECTOR(1024) embedding；SQL Server 2025 支援時建立，須先套用來源 ACL 與相容 profile。'
 ELSE N'原生 VECTOR(768) embedding；SQL Server 2025 支援時建立，須先套用來源 ACL 與相容 profile。' END COLLATE DATABASE_DEFAULT
FROM sys.columns c JOIN sys.tables t ON c.object_id=t.object_id JOIN sys.schemas s ON t.schema_id=s.schema_id
WHERE s.name=N'knowledge' AND t.name=N'Chunks' AND c.name LIKE N'EmbeddingVector%'
UNION ALL
SELECT N'dbo',N'__EFMigrationsHistory',NULL,NULL,N'EF 已套用版本記錄；不可手動刪除以重跑 migration。' WHERE OBJECT_ID(N'dbo.__EFMigrationsHistory') IS NOT NULL
UNION ALL
SELECT N'dbo',N'__EFMigrationsHistory','COLUMN',N'MigrationId',N'已套用的 migration 版本識別碼。' WHERE OBJECT_ID(N'dbo.__EFMigrationsHistory') IS NOT NULL
UNION ALL
SELECT N'dbo',N'__EFMigrationsHistory','COLUMN',N'ProductVersion',N'套用 migration 的 Entity Framework Core 版本。' WHERE OBJECT_ID(N'dbo.__EFMigrationsHistory') IS NOT NULL;
OPEN object_descriptions;
FETCH NEXT FROM object_descriptions INTO @nxDescschema,@nxDesctable,@nxDesckind,@nxDescname,@nxDescdescription;
WHILE @@FETCH_STATUS = 0
BEGIN
 IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description',N'SCHEMA',@nxDescschema,N'TABLE',@nxDesctable,@nxDesckind,@nxDescname))
  EXEC sys.sp_updateextendedproperty @name=N'MS_Description',@value=@nxDescdescription,@level0type=N'SCHEMA',@level0name=@nxDescschema,@level1type=N'TABLE',@level1name=@nxDesctable,@level2type=@nxDesckind,@level2name=@nxDescname;
 ELSE EXEC sys.sp_addextendedproperty @name=N'MS_Description',@value=@nxDescdescription,@level0type=N'SCHEMA',@level0name=@nxDescschema,@level1type=N'TABLE',@level1name=@nxDesctable,@level2type=@nxDesckind,@level2name=@nxDescname;
 FETCH NEXT FROM object_descriptions INTO @nxDescschema,@nxDesctable,@nxDesckind,@nxDescname,@nxDescdescription;
END;
CLOSE object_descriptions; DEALLOCATE object_descriptions;
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_AdAccount",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_LocalAccount",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_AuditEvents_ActorId_Id",
                schema: "operations",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "AdAccount",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AdEnabled",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Enabled",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "FailedLogins",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LocalAccount",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LocalEnabled",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LockedUntil",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ProfileManaged",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SecurityVersion",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ActorId",
                schema: "operations",
                table: "AuditEvents");

            migrationBuilder.AlterTable(
                name: "WebSearches",
                schema: "inference",
                oldComment: "使用者明確啟用的網路搜尋、冪等識別、結果與費用。");

            migrationBuilder.AlterTable(
                name: "Users",
                schema: "identity",
                oldComment: "使用者身分、AD SID 綁定、可用登入方式與工作階段撤銷版本；不保存 AD 密碼。");

            migrationBuilder.AlterTable(
                name: "UserRoles",
                schema: "access",
                oldComment: "使用者與角色的分派關聯。");

            migrationBuilder.AlterTable(
                name: "UserPreferences",
                schema: "identity",
                oldComment: "使用者個人外觀、閱讀、對話操作與通知偏好；不含服務密鑰。");

            migrationBuilder.AlterTable(
                name: "SourceReferences",
                schema: "content",
                oldComment: "外部來源匯入的識別、來源版本與匯入時間。");

            migrationBuilder.AlterTable(
                name: "ShareRecipients",
                schema: "collaboration",
                oldComment: "分享的具名收件人；與原資源 ACL 分開判定。");

            migrationBuilder.AlterTable(
                name: "ShareLinks",
                schema: "collaboration",
                oldComment: "分享版本快照、有效期限、附件選項及撤銷狀態。");

            migrationBuilder.AlterTable(
                name: "RunEvents",
                schema: "inference",
                oldComment: "生成事件的有序 SSE 重播紀錄；完整生成狀態以 GenerationRuns 為準。");

            migrationBuilder.AlterTable(
                name: "Roles",
                schema: "access",
                oldComment: "可分派給使用者的角色；停用後不再提供有效授權。");

            migrationBuilder.AlterTable(
                name: "RoleGroups",
                schema: "access",
                oldComment: "角色所加入的功能群組，集中管理功能及模型政策。");

            migrationBuilder.AlterTable(
                name: "RoleGroupRoles",
                schema: "access",
                oldComment: "角色與功能群組的授權關聯。");

            migrationBuilder.AlterTable(
                name: "RoleGroupFeatures",
                schema: "access",
                oldComment: "功能群組與功能的授權關聯；有效功能取聯集。");

            migrationBuilder.AlterTable(
                name: "Resources",
                schema: "collaboration",
                oldComment: "共用資源的擁有者、種類、階層及版本；作為資料 ACL 邊界。");

            migrationBuilder.AlterTable(
                name: "ResourceMembers",
                schema: "collaboration",
                oldComment: "資源對具名使用者授予的閱讀或編輯權限。");

            migrationBuilder.AlterTable(
                name: "ResourceGroups",
                schema: "collaboration",
                oldComment: "資源對功能群組授予的唯讀權限。");

            migrationBuilder.AlterTable(
                name: "ResourceAttachments",
                schema: "attachments",
                oldComment: "知識庫或專案資源與附件的引用關聯。");

            migrationBuilder.AlterTable(
                name: "RepositoryImports",
                schema: "knowledge",
                oldComment: "程式庫文件匯入所固定的主機、repository、commit 與檔案路徑。");

            migrationBuilder.AlterTable(
                name: "RepositoryConnections",
                schema: "workspace",
                oldComment: "使用者個人的 Gitea 連線及 Data Protection 保護的存取 token。");

            migrationBuilder.AlterTable(
                name: "PromptTemplates",
                schema: "library",
                oldComment: "使用者私人提示詞範本。");

            migrationBuilder.AlterTable(
                name: "ProjectTemplates",
                schema: "projects",
                oldComment: "專案建立範本與預設指令。");

            migrationBuilder.AlterTable(
                name: "Projects",
                schema: "projects",
                oldComment: "專案資源、共用指令及範本版本。");

            migrationBuilder.AlterTable(
                name: "ModelProfiles",
                schema: "inference",
                oldComment: "核准模型的能力、上下文與輸出限制。");

            migrationBuilder.AlterTable(
                name: "ModelPrices",
                schema: "inference",
                oldComment: "依供應商、模型、幣別及成本類型保存的不可變價格版本。");

            migrationBuilder.AlterTable(
                name: "ModelInvocations",
                schema: "inference",
                oldComment: "文字、OCR、embedding 等模型呼叫的狀態與實際用量。");

            migrationBuilder.AlterTable(
                name: "ModelCharges",
                schema: "inference",
                oldComment: "各呼叫當時的價格與用量快照；未知費用保持空值。");

            migrationBuilder.AlterTable(
                name: "Messages",
                schema: "conversations",
                oldComment: "對話訊息樹；提問、回答、重新生成與編輯保留各版本。");

            migrationBuilder.AlterTable(
                name: "MessageFeedback",
                schema: "quality",
                oldComment: "使用者對 AI 回答的私人評分與意見。");

            migrationBuilder.AlterTable(
                name: "MessageCitations",
                schema: "knowledge",
                oldComment: "回答生成當時的知識引用、文件頁碼與摘要快照。");

            migrationBuilder.AlterTable(
                name: "MessageAttachments",
                schema: "attachments",
                oldComment: "訊息與附件的關聯及呈現順序。");

            migrationBuilder.AlterTable(
                name: "GroupModelPolicies",
                schema: "access",
                oldComment: "功能群組的模型白名單、每日生成及附件空間限制。");

            migrationBuilder.AlterTable(
                name: "GenerationRuns",
                schema: "inference",
                oldComment: "聊天生成的持久狀態、冪等請求、執行租約、回答及用量。");

            migrationBuilder.AlterTable(
                name: "Features",
                schema: "access",
                oldComment: "模組註冊的功能入口、顯示名稱、路由及啟用狀態。");

            migrationBuilder.AlterTable(
                name: "EvaluationSets",
                schema: "quality",
                oldComment: "評測題庫、固定測試案例與版本。");

            migrationBuilder.AlterTable(
                name: "EvaluationRuns",
                schema: "quality",
                oldComment: "評測執行的題庫與模型設定快照、背景工作與狀態。");

            migrationBuilder.AlterTable(
                name: "EvaluationResults",
                schema: "quality",
                oldComment: "各案例／模型組合的輸出、指標、用量與人工覆核結果。");

            migrationBuilder.AlterTable(
                name: "Documents",
                schema: "knowledge",
                oldComment: "知識庫文件的原始附件、分析／索引狀態與 embedding profile。");

            migrationBuilder.AlterTable(
                name: "DocumentPages",
                schema: "knowledge",
                oldComment: "文件逐頁擷取的文字、頁碼與 OCR 結果。");

            migrationBuilder.AlterTable(
                name: "Conversations",
                schema: "conversations",
                oldComment: "使用者私人對話、目前訊息分支、收藏封存與自訂指令。");

            migrationBuilder.AlterTable(
                name: "ConversationLabels",
                schema: "conversations",
                oldComment: "使用者對話的分類標籤。");

            migrationBuilder.AlterTable(
                name: "ConversationCollections",
                schema: "knowledge",
                oldComment: "對話選定的知識庫來源關聯。");

            migrationBuilder.AlterTable(
                name: "Collections",
                schema: "knowledge",
                oldComment: "知識庫的資料資源關聯及索引資訊。");

            migrationBuilder.AlterTable(
                name: "Chunks",
                schema: "knowledge",
                oldComment: "知識檢索片段、頁碼、摘要與向量；查詢先套用資料 ACL。");

            migrationBuilder.AlterTable(
                name: "BackgroundJobs",
                schema: "operations",
                oldComment: "文件索引與評測等背景工作的租約、進度、重試與取消狀態。");

            migrationBuilder.AlterTable(
                name: "AuditEvents",
                schema: "operations",
                oldComment: "操作與管理異動稽核；保存實際管理者及有效身分，不記錄密碼或私密內容。");

            migrationBuilder.AlterTable(
                name: "Attachments",
                schema: "attachments",
                oldComment: "使用者附件的原始二進位資料、擷取文字及保留狀態。");

            migrationBuilder.AlterTable(
                name: "Artifacts",
                schema: "content",
                oldComment: "使用者保存的成果文件與目前版本。");

            migrationBuilder.AlterTable(
                name: "ArtifactRevisions",
                schema: "content",
                oldComment: "成果文件不可變版本、內容與作者。");

            migrationBuilder.AlterTable(
                name: "AdministratorBootstraps",
                schema: "access",
                oldComment: "管理者首次啟動授權的永久標記，防止撤銷後重新授權。");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "inference",
                table: "WebSearches",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldComment: "業務執行狀態。");

            migrationBuilder.AlterColumn<Guid>(
                name: "RunId",
                schema: "inference",
                table: "WebSearches",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true,
                oldComment: "關聯生成或評測執行的識別碼。");

            migrationBuilder.AlterColumn<string>(
                name: "ResultsJson",
                schema: "inference",
                table: "WebSearches",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldComment: "搜尋結果的 JSON 快照。");

            migrationBuilder.AlterColumn<string>(
                name: "RequestHash",
                schema: "inference",
                table: "WebSearches",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldComment: "請求內容指紋，用於辨識冪等識別碼衝突。");

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerId",
                schema: "inference",
                table: "WebSearches",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。");

            migrationBuilder.AlterColumn<string>(
                name: "IdempotencyKey",
                schema: "inference",
                table: "WebSearches",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(80)",
                oldMaxLength: 80,
                oldComment: "擁有者範圍內的冪等請求識別，避免重試重複處理。");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "inference",
                table: "WebSearches",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "資料建立時間，採 UTC offset。");

            migrationBuilder.AlterColumn<Guid>(
                name: "ConversationId",
                schema: "inference",
                table: "WebSearches",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "關聯對話的識別碼。");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "inference",
                table: "WebSearches",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "資料的主鍵識別碼。");

            migrationBuilder.AlterColumn<string>(
                name: "Sid",
                schema: "identity",
                table: "Users",
                type: "nvarchar(184)",
                maxLength: 184,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(184)",
                oldMaxLength: 184,
                oldComment: "AD 的不可變 SID；尚未綁定 AD 的手動帳號使用 managed: 識別碼。");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "LastSeenAt",
                schema: "identity",
                table: "Users",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "使用者最近登入／活動時間，採 UTC offset。");

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                schema: "identity",
                table: "Users",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldComment: "使用者或模型的介面顯示名稱。");

            migrationBuilder.AlterColumn<string>(
                name: "Account",
                schema: "identity",
                table: "Users",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldComment: "登入身分顯示帳號；AD 連結後保存目錄提供的帳號。");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "identity",
                table: "Users",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "資料的主鍵識別碼。");

            migrationBuilder.AlterColumn<string>(
                name: "RoleId",
                schema: "access",
                table: "UserRoles",
                type: "nvarchar(64)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldComment: "關聯角色的識別碼。");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "access",
                table: "UserRoles",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "關聯使用者的 Users 主鍵。");

            migrationBuilder.AlterColumn<string>(
                name: "Theme",
                schema: "identity",
                table: "UserPreferences",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(12)",
                oldMaxLength: 12,
                oldComment: "外觀偏好：system、light 或 dark。");

            migrationBuilder.AlterColumn<int>(
                name: "SidebarWidth",
                schema: "identity",
                table: "UserPreferences",
                type: "int",
                nullable: false,
                defaultValue: 264,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 264,
                oldComment: "側欄寬度偏好。");

            migrationBuilder.AlterColumn<bool>(
                name: "SaveLocalDrafts",
                schema: "identity",
                table: "UserPreferences",
                type: "bit",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true,
                oldComment: "是否在瀏覽器按使用者保存草稿。");

            migrationBuilder.AlterColumn<bool>(
                name: "ReducedMotion",
                schema: "identity",
                table: "UserPreferences",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldComment: "是否減少動畫與動態效果。");

            migrationBuilder.AlterColumn<string>(
                name: "ReadingWidth",
                schema: "identity",
                table: "UserPreferences",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "standard",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldDefaultValue: "standard",
                oldComment: "閱讀區寬度偏好。");

            migrationBuilder.AlterColumn<double>(
                name: "ReadingLineHeight",
                schema: "identity",
                table: "UserPreferences",
                type: "float",
                nullable: false,
                defaultValue: 1.8,
                oldClrType: typeof(double),
                oldType: "float",
                oldDefaultValue: 1.8,
                oldComment: "對話閱讀行高倍率。");

            migrationBuilder.AlterColumn<int>(
                name: "ReadingFontSize",
                schema: "identity",
                table: "UserPreferences",
                type: "int",
                nullable: false,
                defaultValue: 17,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 17,
                oldComment: "對話文字大小，以 CSS px 的偏好值記錄。");

            migrationBuilder.AlterColumn<bool>(
                name: "NotifyOnCompletion",
                schema: "identity",
                table: "UserPreferences",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldComment: "是否在背景分頁提醒回答完成。");

            migrationBuilder.AlterColumn<bool>(
                name: "EnterToSend",
                schema: "identity",
                table: "UserPreferences",
                type: "bit",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true,
                oldComment: "是否以 Enter 送出提問；IME 組字不送出。");

            migrationBuilder.AlterColumn<string>(
                name: "Density",
                schema: "identity",
                table: "UserPreferences",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "comfortable",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldDefaultValue: "comfortable",
                oldComment: "介面密度偏好。");

            migrationBuilder.AlterColumn<string>(
                name: "DefaultReasoningEffort",
                schema: "identity",
                table: "UserPreferences",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "auto",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldDefaultValue: "auto",
                oldComment: "偏好的推理強度。");

            migrationBuilder.AlterColumn<string>(
                name: "DefaultModelId",
                schema: "identity",
                table: "UserPreferences",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(160)",
                oldMaxLength: 160,
                oldNullable: true,
                oldComment: "偏好的核准模型識別碼；空值使用伺服器預設。");

            migrationBuilder.AlterColumn<bool>(
                name: "AutoFollow",
                schema: "identity",
                table: "UserPreferences",
                type: "bit",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true,
                oldComment: "生成時是否跟隨最新回答。");

            migrationBuilder.AlterColumn<Guid>(
                name: "NexusUserId",
                schema: "identity",
                table: "UserPreferences",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "個人偏好對應使用者的主鍵與外鍵。");

            migrationBuilder.AlterColumn<string>(
                name: "SourceId",
                schema: "content",
                table: "SourceReferences",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32,
                oldComment: "外部資料來源識別碼。");

            migrationBuilder.AlterColumn<string>(
                name: "Revision",
                schema: "content",
                table: "SourceReferences",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(160)",
                oldMaxLength: 160,
                oldComment: "外部來源或 repository 的固定版本識別。");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ImportedAt",
                schema: "content",
                table: "SourceReferences",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "此來源版本明確匯入的時間。");

            migrationBuilder.AlterColumn<string>(
                name: "ExternalId",
                schema: "content",
                table: "SourceReferences",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(160)",
                oldMaxLength: 160,
                oldComment: "外部來源中的紀錄識別碼。");

            migrationBuilder.AlterColumn<Guid>(
                name: "ArtifactId",
                schema: "content",
                table: "SourceReferences",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "關聯成果文件的識別碼。");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "collaboration",
                table: "ShareRecipients",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "關聯使用者的 Users 主鍵。");

            migrationBuilder.AlterColumn<Guid>(
                name: "ShareId",
                schema: "collaboration",
                table: "ShareRecipients",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "關聯分享的識別碼。");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                schema: "collaboration",
                table: "ShareLinks",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120,
                oldComment: "介面顯示標題。");

            migrationBuilder.AlterColumn<Guid>(
                name: "SourceId",
                schema: "collaboration",
                table: "ShareLinks",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "外部資料來源識別碼。");

            migrationBuilder.AlterColumn<string>(
                name: "SnapshotJson",
                schema: "collaboration",
                table: "ShareLinks",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldComment: "分享時的固定內容快照；不隨後續編輯變動。");

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerId",
                schema: "collaboration",
                table: "ShareLinks",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。");

            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                schema: "collaboration",
                table: "ShareLinks",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(24)",
                oldMaxLength: 24,
                oldComment: "業務操作、資源或成本的種類。");

            migrationBuilder.AlterColumn<bool>(
                name: "IsRevoked",
                schema: "collaboration",
                table: "ShareLinks",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldComment: "分享是否已撤銷。");

            migrationBuilder.AlterColumn<bool>(
                name: "IncludeAttachments",
                schema: "collaboration",
                table: "ShareLinks",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldComment: "是否明確允許分享附件。");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ExpiresAt",
                schema: "collaboration",
                table: "ShareLinks",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "分享或授權到期時間。");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "collaboration",
                table: "ShareLinks",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "資料建立時間，採 UTC offset。");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "collaboration",
                table: "ShareLinks",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "資料的主鍵識別碼。");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                schema: "inference",
                table: "RunEvents",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldComment: "事件的種類。");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "inference",
                table: "RunEvents",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldComment: "業務執行狀態。");

            migrationBuilder.AlterColumn<string>(
                name: "ErrorCode",
                schema: "inference",
                table: "RunEvents",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(80)",
                oldMaxLength: 80,
                oldNullable: true,
                oldComment: "對外安全的錯誤代碼，不含密碼或完整例外。");

            migrationBuilder.AlterColumn<string>(
                name: "Delta",
                schema: "inference",
                table: "RunEvents",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "生成文字增量或完整快照。");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "inference",
                table: "RunEvents",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "資料建立時間，採 UTC offset。");

            migrationBuilder.AlterColumn<long>(
                name: "Sequence",
                schema: "inference",
                table: "RunEvents",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldComment: "事件在同一生成中的遞增序號。");

            migrationBuilder.AlterColumn<Guid>(
                name: "RunId",
                schema: "inference",
                table: "RunEvents",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "關聯生成或評測執行的識別碼。");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "access",
                table: "Roles",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120,
                oldComment: "業務物件的顯示名稱。");

            migrationBuilder.AlterColumn<bool>(
                name: "Enabled",
                schema: "access",
                table: "Roles",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldComment: "是否啟用；停用不刪除歷史資料。");

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                schema: "access",
                table: "Roles",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldComment: "資料的主鍵識別碼。");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "access",
                table: "RoleGroups",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120,
                oldComment: "業務物件的顯示名稱。");

            migrationBuilder.AlterColumn<bool>(
                name: "Enabled",
                schema: "access",
                table: "RoleGroups",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldComment: "是否啟用；停用不刪除歷史資料。");

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                schema: "access",
                table: "RoleGroups",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldComment: "資料的主鍵識別碼。");

            migrationBuilder.AlterColumn<string>(
                name: "GroupId",
                schema: "access",
                table: "RoleGroupRoles",
                type: "nvarchar(64)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldComment: "關聯功能群組的識別碼。");

            migrationBuilder.AlterColumn<string>(
                name: "RoleId",
                schema: "access",
                table: "RoleGroupRoles",
                type: "nvarchar(64)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldComment: "關聯角色的識別碼。");

            migrationBuilder.AlterColumn<string>(
                name: "FeatureId",
                schema: "access",
                table: "RoleGroupFeatures",
                type: "nvarchar(64)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldComment: "關聯功能的識別碼。");

            migrationBuilder.AlterColumn<string>(
                name: "GroupId",
                schema: "access",
                table: "RoleGroupFeatures",
                type: "nvarchar(64)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldComment: "關聯功能群組的識別碼。");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                schema: "collaboration",
                table: "Resources",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "資料最後修改時間，採 UTC offset。");

            migrationBuilder.AlterColumn<Guid>(
                name: "ParentId",
                schema: "collaboration",
                table: "Resources",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true,
                oldComment: "父訊息或父資源識別碼，用於分支或階層繼承。");

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerId",
                schema: "collaboration",
                table: "Resources",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "collaboration",
                table: "Resources",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120,
                oldComment: "業務物件的顯示名稱。");

            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                schema: "collaboration",
                table: "Resources",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(24)",
                oldMaxLength: 24,
                oldComment: "業務操作、資源或成本的種類。");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "collaboration",
                table: "Resources",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldComment: "是否邏輯刪除；不自動刪除歷史紀錄。");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "collaboration",
                table: "Resources",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "資料建立時間，採 UTC offset。");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "collaboration",
                table: "Resources",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "資料的主鍵識別碼。");

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                schema: "collaboration",
                table: "ResourceMembers",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(12)",
                oldMaxLength: 12,
                oldComment: "訊息角色（user／assistant）或資源成員的閱讀／編輯權限。");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "collaboration",
                table: "ResourceMembers",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "關聯使用者的 Users 主鍵。");

            migrationBuilder.AlterColumn<Guid>(
                name: "ResourceId",
                schema: "collaboration",
                table: "ResourceMembers",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "關聯或稽核對象的業務資源識別碼。");

            migrationBuilder.AlterColumn<string>(
                name: "GroupId",
                schema: "collaboration",
                table: "ResourceGroups",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldComment: "關聯功能群組的識別碼。");

            migrationBuilder.AlterColumn<Guid>(
                name: "ResourceId",
                schema: "collaboration",
                table: "ResourceGroups",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "關聯或稽核對象的業務資源識別碼。");

            migrationBuilder.AlterColumn<Guid>(
                name: "AttachmentId",
                schema: "attachments",
                table: "ResourceAttachments",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "引用的附件識別碼。");

            migrationBuilder.AlterColumn<Guid>(
                name: "ResourceId",
                schema: "attachments",
                table: "ResourceAttachments",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "關聯或稽核對象的業務資源識別碼。");

            migrationBuilder.AlterColumn<string>(
                name: "Repository",
                schema: "knowledge",
                table: "RepositoryImports",
                type: "nvarchar(201)",
                maxLength: 201,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(201)",
                oldMaxLength: 201,
                oldComment: "Gitea repository 的 owner/name 識別。");

            migrationBuilder.AlterColumn<string>(
                name: "Path",
                schema: "knowledge",
                table: "RepositoryImports",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldComment: "repository 內的檔案路徑，不是伺服器路徑。");

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerId",
                schema: "knowledge",
                table: "RepositoryImports",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。");

            migrationBuilder.AlterColumn<Guid>(
                name: "DocumentId",
                schema: "knowledge",
                table: "RepositoryImports",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "關聯知識文件的識別碼。");

            migrationBuilder.AlterColumn<string>(
                name: "Commit",
                schema: "knowledge",
                table: "RepositoryImports",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldComment: "匯入當時固定的 commit SHA。");

            migrationBuilder.AlterColumn<Guid>(
                name: "CollectionId",
                schema: "knowledge",
                table: "RepositoryImports",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "關聯知識庫的識別碼。");

            migrationBuilder.AlterColumn<string>(
                name: "BaseUrl",
                schema: "knowledge",
                table: "RepositoryImports",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldComment: "Gitea 連線主機位址。");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "knowledge",
                table: "RepositoryImports",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "資料的主鍵識別碼。");

            migrationBuilder.AlterColumn<string>(
                name: "ProtectedToken",
                schema: "workspace",
                table: "RepositoryConnections",
                type: "nvarchar(max)",
                maxLength: 4096,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldMaxLength: 4096,
                oldComment: "以 ASP.NET Data Protection 保護的外部 token；不可在 API、稽核或日誌回傳。");

            migrationBuilder.AlterColumn<string>(
                name: "Login",
                schema: "workspace",
                table: "RepositoryConnections",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldComment: "外部服務的使用者登入名稱。");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ConnectedAt",
                schema: "workspace",
                table: "RepositoryConnections",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "使用者建立外部服務連線的時間。");

            migrationBuilder.AlterColumn<string>(
                name: "BaseUrl",
                schema: "workspace",
                table: "RepositoryConnections",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldComment: "Gitea 連線主機位址。");

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerId",
                schema: "workspace",
                table: "RepositoryConnections",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                schema: "library",
                table: "PromptTemplates",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "資料最後修改時間，採 UTC offset。");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                schema: "library",
                table: "PromptTemplates",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(80)",
                oldMaxLength: 80,
                oldComment: "介面顯示標題。");

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerId",
                schema: "library",
                table: "PromptTemplates",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。");

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                schema: "library",
                table: "PromptTemplates",
                type: "nvarchar(max)",
                maxLength: 12000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldMaxLength: 12000,
                oldComment: "訊息、版本或生成的文字內容。");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "library",
                table: "PromptTemplates",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "資料的主鍵識別碼。");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                schema: "projects",
                table: "ProjectTemplates",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(80)",
                oldMaxLength: 80,
                oldComment: "介面顯示標題。");

            migrationBuilder.AlterColumn<Guid>(
                name: "ProjectId",
                schema: "projects",
                table: "ProjectTemplates",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "關聯專案的識別碼。");

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                schema: "projects",
                table: "ProjectTemplates",
                type: "nvarchar(max)",
                maxLength: 12000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldMaxLength: 12000,
                oldComment: "訊息、版本或生成的文字內容。");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "projects",
                table: "ProjectTemplates",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "資料的主鍵識別碼。");

            migrationBuilder.AlterColumn<int>(
                name: "Version",
                schema: "projects",
                table: "Projects",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldComment: "業務版本號，用於歷史或樂觀並行控制。");

            migrationBuilder.AlterColumn<bool>(
                name: "IsArchived",
                schema: "projects",
                table: "Projects",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldComment: "是否封存對話；封存後不再接受新的生成。");

            migrationBuilder.AlterColumn<string>(
                name: "Instructions",
                schema: "projects",
                table: "Projects",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000,
                oldComment: "專案共用指令。");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "projects",
                table: "Projects",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldComment: "業務物件的用途說明。");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "projects",
                table: "Projects",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "資料的主鍵識別碼。");

            migrationBuilder.AlterColumn<bool>(
                name: "SupportsUsage",
                schema: "inference",
                table: "ModelProfiles",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldComment: "模型是否會回報實際用量。");

            migrationBuilder.AlterColumn<bool>(
                name: "SupportsStreaming",
                schema: "inference",
                table: "ModelProfiles",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldComment: "模型是否支援串流輸出。");

            migrationBuilder.AlterColumn<int>(
                name: "MaxOutputTokens",
                schema: "inference",
                table: "ModelProfiles",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldComment: "模型核准的最大輸出 tokens。");

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                schema: "inference",
                table: "ModelProfiles",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120,
                oldComment: "使用者或模型的介面顯示名稱。");

            migrationBuilder.AlterColumn<int>(
                name: "ContextTokens",
                schema: "inference",
                table: "ModelProfiles",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldComment: "模型上下文容量，以 tokens 計。");

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                schema: "inference",
                table: "ModelProfiles",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(160)",
                oldMaxLength: 160,
                oldComment: "資料的主鍵識別碼。");

            migrationBuilder.AlterColumn<string>(
                name: "RequestCharge",
                schema: "inference",
                table: "ModelPrices",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldComment: "搜尋服務每次請求的費用。");

            migrationBuilder.AlterColumn<string>(
                name: "Provider",
                schema: "inference",
                table: "ModelPrices",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32,
                oldComment: "模型或搜尋服務供應商識別碼。");

            migrationBuilder.AlterColumn<decimal>(
                name: "PerRequest",
                schema: "inference",
                table: "ModelPrices",
                type: "decimal(20,8)",
                precision: 20,
                scale: 8,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(20,8)",
                oldPrecision: 20,
                oldScale: 8,
                oldComment: "每次呼叫的固定單價。");

            migrationBuilder.AlterColumn<decimal>(
                name: "OutputPerMillion",
                schema: "inference",
                table: "ModelPrices",
                type: "decimal(20,8)",
                precision: 20,
                scale: 8,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(20,8)",
                oldPrecision: 20,
                oldScale: 8,
                oldComment: "每百萬輸出 tokens 的單價。");

            migrationBuilder.AlterColumn<string>(
                name: "Note",
                schema: "inference",
                table: "ModelPrices",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldComment: "使用者提供的補充說明。");

            migrationBuilder.AlterColumn<string>(
                name: "ModelId",
                schema: "inference",
                table: "ModelPrices",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(160)",
                oldMaxLength: 160,
                oldComment: "核准模型的內部識別碼。");

            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                schema: "inference",
                table: "ModelPrices",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldComment: "業務操作、資源或成本的種類。");

            migrationBuilder.AlterColumn<decimal>(
                name: "InputPerMillion",
                schema: "inference",
                table: "ModelPrices",
                type: "decimal(20,8)",
                precision: 20,
                scale: 8,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(20,8)",
                oldPrecision: 20,
                oldScale: 8,
                oldComment: "每百萬輸入 tokens 的單價。");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "EffectiveAt",
                schema: "inference",
                table: "ModelPrices",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "此價格版本開始生效的時間。");

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                schema: "inference",
                table: "ModelPrices",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3,
                oldComment: "費用幣別代碼；不同幣別不可直接合計。");

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedBy",
                schema: "inference",
                table: "ModelPrices",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "建立紀錄的使用者識別碼。");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "inference",
                table: "ModelPrices",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "資料建立時間，採 UTC offset。");

            migrationBuilder.AlterColumn<decimal>(
                name: "CachedInputPerMillion",
                schema: "inference",
                table: "ModelPrices",
                type: "decimal(20,8)",
                precision: 20,
                scale: 8,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(20,8)",
                oldPrecision: 20,
                oldScale: 8,
                oldComment: "每百萬快取輸入 tokens 的單價。");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "inference",
                table: "ModelPrices",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "資料的主鍵識別碼。");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "inference",
                table: "ModelInvocations",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldComment: "業務執行狀態。");

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerId",
                schema: "inference",
                table: "ModelInvocations",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。");

            migrationBuilder.AlterColumn<long>(
                name: "OutputTokens",
                schema: "inference",
                table: "ModelInvocations",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true,
                oldComment: "模型回報的輸出 tokens；未知保持空值。");

            migrationBuilder.AlterColumn<string>(
                name: "ModelId",
                schema: "inference",
                table: "ModelInvocations",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(160)",
                oldMaxLength: 160,
                oldComment: "核准模型的內部識別碼。");

            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                schema: "inference",
                table: "ModelInvocations",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32,
                oldComment: "業務操作、資源或成本的種類。");

            migrationBuilder.AlterColumn<long>(
                name: "InputTokens",
                schema: "inference",
                table: "ModelInvocations",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true,
                oldComment: "模型回報的輸入 tokens；未知保持空值。");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "inference",
                table: "ModelInvocations",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "資料建立時間，採 UTC offset。");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "inference",
                table: "ModelInvocations",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "資料的主鍵識別碼。");

            migrationBuilder.AlterColumn<bool>(
                name: "UsageComplete",
                schema: "inference",
                table: "ModelCharges",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldComment: "本次呼叫是否有完整且可計費的實際用量。");

            migrationBuilder.AlterColumn<string>(
                name: "State",
                schema: "inference",
                table: "ModelCharges",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(24)",
                oldMaxLength: 24,
                oldComment: "業務物件的生命週期狀態。");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "StartedAt",
                schema: "inference",
                table: "ModelCharges",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true,
                oldComment: "工作開始執行時間。");

            migrationBuilder.AlterColumn<string>(
                name: "RequestCharge",
                schema: "inference",
                table: "ModelCharges",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldComment: "搜尋服務每次請求的費用。");

            migrationBuilder.AlterColumn<long>(
                name: "ReasoningTokens",
                schema: "inference",
                table: "ModelCharges",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true,
                oldComment: "模型回報的推理 tokens。");

            migrationBuilder.AlterColumn<string>(
                name: "Provider",
                schema: "inference",
                table: "ModelCharges",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32,
                oldComment: "模型或搜尋服務供應商識別碼。");

            migrationBuilder.AlterColumn<Guid>(
                name: "PriceId",
                schema: "inference",
                table: "ModelCharges",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true,
                oldComment: "此呼叫採用的不可變價格版本。");

            migrationBuilder.AlterColumn<decimal>(
                name: "PerRequest",
                schema: "inference",
                table: "ModelCharges",
                type: "decimal(20,8)",
                precision: 20,
                scale: 8,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(20,8)",
                oldPrecision: 20,
                oldScale: 8,
                oldComment: "每次呼叫的固定單價。");

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerId",
                schema: "inference",
                table: "ModelCharges",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。");

            migrationBuilder.AlterColumn<long>(
                name: "OutputTokens",
                schema: "inference",
                table: "ModelCharges",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true,
                oldComment: "模型回報的輸出 tokens；未知保持空值。");

            migrationBuilder.AlterColumn<decimal>(
                name: "OutputPerMillion",
                schema: "inference",
                table: "ModelCharges",
                type: "decimal(20,8)",
                precision: 20,
                scale: 8,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(20,8)",
                oldPrecision: 20,
                oldScale: 8,
                oldComment: "每百萬輸出 tokens 的單價。");

            migrationBuilder.AlterColumn<string>(
                name: "Outcome",
                schema: "inference",
                table: "ModelCharges",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldComment: "搜尋或處理操作的結果分類。");

            migrationBuilder.AlterColumn<string>(
                name: "Operation",
                schema: "inference",
                table: "ModelCharges",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32,
                oldComment: "模型呼叫或背景工作的操作類型。");

            migrationBuilder.AlterColumn<string>(
                name: "ModelId",
                schema: "inference",
                table: "ModelCharges",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(160)",
                oldMaxLength: 160,
                oldComment: "核准模型的內部識別碼。");

            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                schema: "inference",
                table: "ModelCharges",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldComment: "業務操作、資源或成本的種類。");

            migrationBuilder.AlterColumn<long>(
                name: "InputTokens",
                schema: "inference",
                table: "ModelCharges",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true,
                oldComment: "模型回報的輸入 tokens；未知保持空值。");

            migrationBuilder.AlterColumn<decimal>(
                name: "InputPerMillion",
                schema: "inference",
                table: "ModelCharges",
                type: "decimal(20,8)",
                precision: 20,
                scale: 8,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(20,8)",
                oldPrecision: 20,
                oldScale: 8,
                oldComment: "每百萬輸入 tokens 的單價。");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "FinishedAt",
                schema: "inference",
                table: "ModelCharges",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true,
                oldComment: "工作結束時間。");

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                schema: "inference",
                table: "ModelCharges",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3,
                oldComment: "費用幣別代碼；不同幣別不可直接合計。");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "inference",
                table: "ModelCharges",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "資料建立時間，採 UTC offset。");

            migrationBuilder.AlterColumn<Guid>(
                name: "ConversationId",
                schema: "inference",
                table: "ModelCharges",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true,
                oldComment: "關聯對話的識別碼。");

            migrationBuilder.AlterColumn<long>(
                name: "CachedInputTokens",
                schema: "inference",
                table: "ModelCharges",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true,
                oldComment: "模型回報的快取輸入 tokens。");

            migrationBuilder.AlterColumn<decimal>(
                name: "CachedInputPerMillion",
                schema: "inference",
                table: "ModelCharges",
                type: "decimal(20,8)",
                precision: 20,
                scale: 8,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(20,8)",
                oldPrecision: 20,
                oldScale: 8,
                oldComment: "每百萬快取輸入 tokens 的單價。");

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                schema: "inference",
                table: "ModelCharges",
                type: "decimal(20,8)",
                precision: 20,
                scale: 8,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(20,8)",
                oldPrecision: 20,
                oldScale: 8,
                oldNullable: true,
                oldComment: "此呼叫的已知費用；未知保持空值。");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "inference",
                table: "ModelCharges",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "資料的主鍵識別碼。");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "conversations",
                table: "Messages",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldComment: "業務執行狀態。");

            migrationBuilder.AlterColumn<Guid>(
                name: "RunId",
                schema: "conversations",
                table: "Messages",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true,
                oldComment: "關聯生成或評測執行的識別碼。");

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                schema: "conversations",
                table: "Messages",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldComment: "訊息角色（user／assistant）或資源成員的閱讀／編輯權限。");

            migrationBuilder.AlterColumn<Guid>(
                name: "ParentId",
                schema: "conversations",
                table: "Messages",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true,
                oldComment: "父訊息或父資源識別碼，用於分支或階層繼承。");

            migrationBuilder.AlterColumn<string>(
                name: "ModelId",
                schema: "conversations",
                table: "Messages",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(160)",
                oldMaxLength: 160,
                oldNullable: true,
                oldComment: "核准模型的內部識別碼。");

            migrationBuilder.AlterColumn<string>(
                name: "ErrorCode",
                schema: "conversations",
                table: "Messages",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(80)",
                oldMaxLength: 80,
                oldNullable: true,
                oldComment: "對外安全的錯誤代碼，不含密碼或完整例外。");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "conversations",
                table: "Messages",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "資料建立時間，採 UTC offset。");

            migrationBuilder.AlterColumn<Guid>(
                name: "ConversationId",
                schema: "conversations",
                table: "Messages",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "關聯對話的識別碼。");

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                schema: "conversations",
                table: "Messages",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldComment: "訊息、版本或生成的文字內容。");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "conversations",
                table: "Messages",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "資料的主鍵識別碼。");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                schema: "quality",
                table: "MessageFeedback",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "資料最後修改時間，採 UTC offset。");

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                schema: "quality",
                table: "MessageFeedback",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(24)",
                oldMaxLength: 24,
                oldComment: "回饋或操作理由。");

            migrationBuilder.AlterColumn<int>(
                name: "Rating",
                schema: "quality",
                table: "MessageFeedback",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldComment: "使用者對回答的評分。");

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerId",
                schema: "quality",
                table: "MessageFeedback",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。");

            migrationBuilder.AlterColumn<string>(
                name: "Note",
                schema: "quality",
                table: "MessageFeedback",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldComment: "使用者提供的補充說明。");

            migrationBuilder.AlterColumn<Guid>(
                name: "MessageId",
                schema: "quality",
                table: "MessageFeedback",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "關聯訊息的識別碼。");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                schema: "knowledge",
                table: "MessageCitations",
                type: "nvarchar(180)",
                maxLength: 180,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(180)",
                oldMaxLength: 180,
                oldComment: "介面顯示標題。");

            migrationBuilder.AlterColumn<int>(
                name: "PageNumber",
                schema: "knowledge",
                table: "MessageCitations",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldComment: "文件頁碼，從 1 開始。");

            migrationBuilder.AlterColumn<string>(
                name: "Excerpt",
                schema: "knowledge",
                table: "MessageCitations",
                type: "nvarchar(800)",
                maxLength: 800,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(800)",
                oldMaxLength: 800,
                oldComment: "檢索或引用時保存的文字摘要。");

            migrationBuilder.AlterColumn<Guid>(
                name: "DocumentId",
                schema: "knowledge",
                table: "MessageCitations",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "關聯知識文件的識別碼。");

            migrationBuilder.AlterColumn<int>(
                name: "Number",
                schema: "knowledge",
                table: "MessageCitations",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldComment: "回答引用的順序編號。");

            migrationBuilder.AlterColumn<Guid>(
                name: "MessageId",
                schema: "knowledge",
                table: "MessageCitations",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "關聯訊息的識別碼。");

            migrationBuilder.AlterColumn<Guid>(
                name: "AttachmentId",
                schema: "attachments",
                table: "MessageAttachments",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "引用的附件識別碼。");

            migrationBuilder.AlterColumn<Guid>(
                name: "MessageId",
                schema: "attachments",
                table: "MessageAttachments",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "關聯訊息的識別碼。");

            migrationBuilder.AlterColumn<long>(
                name: "StoredAttachmentLimitBytes",
                schema: "access",
                table: "GroupModelPolicies",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true,
                oldComment: "個人附件儲存上限，以 bytes 計；群組限制取最低值。");

            migrationBuilder.AlterColumn<int>(
                name: "DailyRequestLimit",
                schema: "access",
                table: "GroupModelPolicies",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true,
                oldComment: "每日生成次數上限；群組限制取最低值，UTC 午夜重設。");

            migrationBuilder.AlterColumn<string>(
                name: "AllowedModelsJson",
                schema: "access",
                table: "GroupModelPolicies",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000,
                oldNullable: true,
                oldComment: "模型白名單 JSON；空值不增加限制，空陣列禁止生成。");

            migrationBuilder.AlterColumn<string>(
                name: "GroupId",
                schema: "access",
                table: "GroupModelPolicies",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldComment: "關聯功能群組的識別碼。");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserMessageId",
                schema: "inference",
                table: "GenerationRuns",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "此次生成的使用者提問訊息識別碼。");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "inference",
                table: "GenerationRuns",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldComment: "業務執行狀態。");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "StartedAt",
                schema: "inference",
                table: "GenerationRuns",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true,
                oldComment: "工作開始執行時間。");

            migrationBuilder.AlterColumn<string>(
                name: "RequestHash",
                schema: "inference",
                table: "GenerationRuns",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldComment: "請求內容指紋，用於辨識冪等識別碼衝突。");

            migrationBuilder.AlterColumn<string>(
                name: "ParametersJson",
                schema: "inference",
                table: "GenerationRuns",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldComment: "執行參數的 JSON 快照，不含服務密鑰。");

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerId",
                schema: "inference",
                table: "GenerationRuns",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。");

            migrationBuilder.AlterColumn<long>(
                name: "OutputTokens",
                schema: "inference",
                table: "GenerationRuns",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true,
                oldComment: "模型回報的輸出 tokens；未知保持空值。");

            migrationBuilder.AlterColumn<string>(
                name: "ModelId",
                schema: "inference",
                table: "GenerationRuns",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(160)",
                oldMaxLength: 160,
                oldComment: "核准模型的內部識別碼。");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "LeaseExpiresAt",
                schema: "inference",
                table: "GenerationRuns",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true,
                oldComment: "生成 executor 租約的到期時間。");

            migrationBuilder.AlterColumn<long>(
                name: "LastSequence",
                schema: "inference",
                table: "GenerationRuns",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldComment: "最後已持久化的生成事件序號。");

            migrationBuilder.AlterColumn<long>(
                name: "InputTokens",
                schema: "inference",
                table: "GenerationRuns",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true,
                oldComment: "模型回報的輸入 tokens；未知保持空值。");

            migrationBuilder.AlterColumn<string>(
                name: "IdempotencyKey",
                schema: "inference",
                table: "GenerationRuns",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(80)",
                oldMaxLength: 80,
                oldComment: "擁有者範圍內的冪等請求識別，避免重試重複處理。");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "FinishedAt",
                schema: "inference",
                table: "GenerationRuns",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true,
                oldComment: "工作結束時間。");

            migrationBuilder.AlterColumn<Guid>(
                name: "ExecutorId",
                schema: "inference",
                table: "GenerationRuns",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true,
                oldComment: "處理此次生成的伺服器程序識別碼。");

            migrationBuilder.AlterColumn<string>(
                name: "ErrorCode",
                schema: "inference",
                table: "GenerationRuns",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(80)",
                oldMaxLength: 80,
                oldNullable: true,
                oldComment: "對外安全的錯誤代碼，不含密碼或完整例外。");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "inference",
                table: "GenerationRuns",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "資料建立時間，採 UTC offset。");

            migrationBuilder.AlterColumn<Guid>(
                name: "ConversationId",
                schema: "inference",
                table: "GenerationRuns",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "關聯對話的識別碼。");

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                schema: "inference",
                table: "GenerationRuns",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldComment: "訊息、版本或生成的文字內容。");

            migrationBuilder.AlterColumn<Guid>(
                name: "AssistantMessageId",
                schema: "inference",
                table: "GenerationRuns",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "此次生成的 AI 回答訊息識別碼。");

            migrationBuilder.AlterColumn<Guid>(
                name: "ActiveOwnerId",
                schema: "inference",
                table: "GenerationRuns",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true,
                oldComment: "仍在執行的擁有者；filtered unique index 限制每人一個生成。");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "inference",
                table: "GenerationRuns",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "資料的主鍵識別碼。");

            migrationBuilder.AlterColumn<int>(
                name: "SortOrder",
                schema: "access",
                table: "Features",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldComment: "介面顯示順序；數值越小越前。");

            migrationBuilder.AlterColumn<string>(
                name: "Route",
                schema: "access",
                table: "Features",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(160)",
                oldMaxLength: 160,
                oldComment: "功能入口的本站路由；API 授權仍由後端政策判定。");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "access",
                table: "Features",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120,
                oldComment: "業務物件的顯示名稱。");

            migrationBuilder.AlterColumn<bool>(
                name: "Enabled",
                schema: "access",
                table: "Features",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldComment: "是否啟用；停用不刪除歷史資料。");

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                schema: "access",
                table: "Features",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldComment: "資料的主鍵識別碼。");

            migrationBuilder.AlterColumn<int>(
                name: "Version",
                schema: "quality",
                table: "EvaluationSets",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldComment: "業務版本號，用於歷史或樂觀並行控制。");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "quality",
                table: "EvaluationSets",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldComment: "業務物件的用途說明。");

            migrationBuilder.AlterColumn<string>(
                name: "CasesJson",
                schema: "quality",
                table: "EvaluationSets",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldComment: "固定評測案例的 JSON 快照。");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "quality",
                table: "EvaluationSets",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "資料的主鍵識別碼。");

            migrationBuilder.AlterColumn<string>(
                name: "VariantsJson",
                schema: "quality",
                table: "EvaluationRuns",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldComment: "評測模型／參數組合的 JSON 快照。");

            migrationBuilder.AlterColumn<int>(
                name: "SetVersion",
                schema: "quality",
                table: "EvaluationRuns",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldComment: "評測執行當時的題庫版本。");

            migrationBuilder.AlterColumn<string>(
                name: "SetTitle",
                schema: "quality",
                table: "EvaluationRuns",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120,
                oldComment: "評測執行當時的題庫名稱快照。");

            migrationBuilder.AlterColumn<Guid>(
                name: "SetId",
                schema: "quality",
                table: "EvaluationRuns",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "評測題庫識別碼。");

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerId",
                schema: "quality",
                table: "EvaluationRuns",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。");

            migrationBuilder.AlterColumn<Guid>(
                name: "JobId",
                schema: "quality",
                table: "EvaluationRuns",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "關聯背景工作識別碼。");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "quality",
                table: "EvaluationRuns",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "資料建立時間，採 UTC offset。");

            migrationBuilder.AlterColumn<string>(
                name: "CasesJson",
                schema: "quality",
                table: "EvaluationRuns",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldComment: "固定評測案例的 JSON 快照。");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "quality",
                table: "EvaluationRuns",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "資料的主鍵識別碼。");

            migrationBuilder.AlterColumn<bool>(
                name: "Truncated",
                schema: "quality",
                table: "EvaluationResults",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldComment: "評測輸出是否因上限截斷。");

            migrationBuilder.AlterColumn<Guid>(
                name: "ReviewerId",
                schema: "quality",
                table: "EvaluationResults",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true,
                oldComment: "人工覆核者的使用者識別碼。");

            migrationBuilder.AlterColumn<int>(
                name: "ReviewScore",
                schema: "quality",
                table: "EvaluationResults",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true,
                oldComment: "人工覆核分數；未覆核保持空值。");

            migrationBuilder.AlterColumn<string>(
                name: "ReviewNote",
                schema: "quality",
                table: "EvaluationResults",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldComment: "人工覆核意見。");

            migrationBuilder.AlterColumn<int>(
                name: "RequiredTotal",
                schema: "quality",
                table: "EvaluationResults",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldComment: "必要條件總數。");

            migrationBuilder.AlterColumn<int>(
                name: "RequiredMatches",
                schema: "quality",
                table: "EvaluationResults",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldComment: "命中的必要條件數。");

            migrationBuilder.AlterColumn<long>(
                name: "OutputTokens",
                schema: "quality",
                table: "EvaluationResults",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true,
                oldComment: "模型回報的輸出 tokens；未知保持空值。");

            migrationBuilder.AlterColumn<string>(
                name: "Output",
                schema: "quality",
                table: "EvaluationResults",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldComment: "評測模型的實際回答。");

            migrationBuilder.AlterColumn<long>(
                name: "InputTokens",
                schema: "quality",
                table: "EvaluationResults",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true,
                oldComment: "模型回報的輸入 tokens；未知保持空值。");

            migrationBuilder.AlterColumn<int>(
                name: "ForbiddenMatches",
                schema: "quality",
                table: "EvaluationResults",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldComment: "命中的禁止條件數。");

            migrationBuilder.AlterColumn<long>(
                name: "ElapsedMs",
                schema: "quality",
                table: "EvaluationResults",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldComment: "執行耗時，以毫秒計。");

            migrationBuilder.AlterColumn<int>(
                name: "VariantIndex",
                schema: "quality",
                table: "EvaluationResults",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldComment: "評測模型／參數組合的從零開始索引。");

            migrationBuilder.AlterColumn<int>(
                name: "CaseIndex",
                schema: "quality",
                table: "EvaluationResults",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldComment: "評測案例的從零開始索引。");

            migrationBuilder.AlterColumn<Guid>(
                name: "RunId",
                schema: "quality",
                table: "EvaluationResults",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "關聯生成或評測執行的識別碼。");

            migrationBuilder.AlterColumn<string>(
                name: "Warning",
                schema: "knowledge",
                table: "Documents",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true,
                oldComment: "處理過程中的非致命提示。");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "knowledge",
                table: "Documents",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldComment: "業務執行狀態。");

            migrationBuilder.AlterColumn<int>(
                name: "PageCount",
                schema: "knowledge",
                table: "Documents",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldComment: "文件總頁數。");

            migrationBuilder.AlterColumn<Guid>(
                name: "JobId",
                schema: "knowledge",
                table: "Documents",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true,
                oldComment: "關聯背景工作識別碼。");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "knowledge",
                table: "Documents",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldComment: "是否邏輯刪除；不自動刪除歷史紀錄。");

            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                schema: "knowledge",
                table: "Documents",
                type: "nvarchar(180)",
                maxLength: 180,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(180)",
                oldMaxLength: 180,
                oldComment: "原始附件檔名，不作為伺服器儲存路徑。");

            migrationBuilder.AlterColumn<string>(
                name: "EmbeddingProfile",
                schema: "knowledge",
                table: "Documents",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true,
                oldComment: "向量模型、維度與前處理的版本指紋；不混用不同 profile。");

            migrationBuilder.AlterColumn<string>(
                name: "ContentType",
                schema: "knowledge",
                table: "Documents",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(80)",
                oldMaxLength: 80,
                oldComment: "核准的 MIME 型別。");

            migrationBuilder.AlterColumn<Guid>(
                name: "CollectionId",
                schema: "knowledge",
                table: "Documents",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true,
                oldComment: "關聯知識庫的識別碼。");

            migrationBuilder.AlterColumn<int>(
                name: "ChunkCount",
                schema: "knowledge",
                table: "Documents",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldComment: "文件已建立的檢索片段數。");

            migrationBuilder.AlterColumn<Guid>(
                name: "AttachmentId",
                schema: "knowledge",
                table: "Documents",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true,
                oldComment: "引用的附件識別碼。");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "knowledge",
                table: "Documents",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "資料的主鍵識別碼。");

            migrationBuilder.AlterColumn<string>(
                name: "Text",
                schema: "knowledge",
                table: "DocumentPages",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldComment: "文件頁面／片段的擷取文字。");

            migrationBuilder.AlterColumn<bool>(
                name: "NeedsReview",
                schema: "knowledge",
                table: "DocumentPages",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldComment: "此評測結果是否需要人工覆核。");

            migrationBuilder.AlterColumn<string>(
                name: "Extraction",
                schema: "knowledge",
                table: "DocumentPages",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldComment: "附件文字擷取方法或結果。");

            migrationBuilder.AlterColumn<int>(
                name: "PageNumber",
                schema: "knowledge",
                table: "DocumentPages",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldComment: "文件頁碼，從 1 開始。");

            migrationBuilder.AlterColumn<Guid>(
                name: "DocumentId",
                schema: "knowledge",
                table: "DocumentPages",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "關聯知識文件的識別碼。");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                schema: "conversations",
                table: "Conversations",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "資料最後修改時間，採 UTC offset。");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                schema: "conversations",
                table: "Conversations",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120,
                oldComment: "介面顯示標題。");

            migrationBuilder.AlterColumn<string>(
                name: "SystemInstruction",
                schema: "conversations",
                table: "Conversations",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000,
                oldComment: "對話專用的回答指令。");

            migrationBuilder.AlterColumn<Guid>(
                name: "ProjectId",
                schema: "conversations",
                table: "Conversations",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true,
                oldComment: "關聯專案的識別碼。");

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerId",
                schema: "conversations",
                table: "Conversations",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。");

            migrationBuilder.AlterColumn<bool>(
                name: "IsFavorite",
                schema: "conversations",
                table: "Conversations",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldComment: "是否標記收藏。");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                schema: "conversations",
                table: "Conversations",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldComment: "是否邏輯刪除；不自動刪除歷史紀錄。");

            migrationBuilder.AlterColumn<bool>(
                name: "IsArchived",
                schema: "conversations",
                table: "Conversations",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldComment: "是否封存對話；封存後不再接受新的生成。");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "conversations",
                table: "Conversations",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "資料建立時間，採 UTC offset。");

            migrationBuilder.AlterColumn<Guid>(
                name: "ActiveLeafId",
                schema: "conversations",
                table: "Conversations",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true,
                oldComment: "對話目前顯示分支的最後訊息識別碼。");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "conversations",
                table: "Conversations",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "資料的主鍵識別碼。");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "conversations",
                table: "ConversationLabels",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(24)",
                oldMaxLength: 24,
                oldComment: "業務物件的顯示名稱。");

            migrationBuilder.AlterColumn<Guid>(
                name: "ConversationId",
                schema: "conversations",
                table: "ConversationLabels",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "關聯對話的識別碼。");

            migrationBuilder.AlterColumn<Guid>(
                name: "CollectionId",
                schema: "knowledge",
                table: "ConversationCollections",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "關聯知識庫的識別碼。");

            migrationBuilder.AlterColumn<Guid>(
                name: "ConversationId",
                schema: "knowledge",
                table: "ConversationCollections",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "關聯對話的識別碼。");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "knowledge",
                table: "Collections",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldComment: "業務物件的用途說明。");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "knowledge",
                table: "Collections",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "資料的主鍵識別碼。");

            migrationBuilder.AlterColumn<string>(
                name: "Text",
                schema: "knowledge",
                table: "Chunks",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldComment: "文件頁面／片段的擷取文字。");

            migrationBuilder.AlterColumn<int>(
                name: "PageNumber",
                schema: "knowledge",
                table: "Chunks",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldComment: "文件頁碼，從 1 開始。");

            migrationBuilder.AlterColumn<int>(
                name: "Ordinal",
                schema: "knowledge",
                table: "Chunks",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldComment: "同一父物件內的呈現順序。");

            migrationBuilder.AlterColumn<string>(
                name: "EmbeddingProfile",
                schema: "knowledge",
                table: "Chunks",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true,
                oldComment: "向量模型、維度與前處理的版本指紋；不混用不同 profile。");

            migrationBuilder.AlterColumn<string>(
                name: "EmbeddingJson",
                schema: "knowledge",
                table: "Chunks",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "正規化向量的 JSON 表示；與 embedding profile 一起判斷相容性。");

            migrationBuilder.AlterColumn<Guid>(
                name: "DocumentId",
                schema: "knowledge",
                table: "Chunks",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "關聯知識文件的識別碼。");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "knowledge",
                table: "Chunks",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "資料的主鍵識別碼。");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                schema: "operations",
                table: "BackgroundJobs",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "資料最後修改時間，採 UTC offset。");

            migrationBuilder.AlterColumn<int>(
                name: "TotalUnits",
                schema: "operations",
                table: "BackgroundJobs",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true,
                oldComment: "已知的總工作單位數；未知不表示百分比。");

            migrationBuilder.AlterColumn<Guid>(
                name: "SubjectId",
                schema: "operations",
                table: "BackgroundJobs",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "操作所關聯的業務對象識別碼。");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "operations",
                table: "BackgroundJobs",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldComment: "業務執行狀態。");

            migrationBuilder.AlterColumn<string>(
                name: "Stage",
                schema: "operations",
                table: "BackgroundJobs",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120,
                oldComment: "背景工作目前階段。");

            migrationBuilder.AlterColumn<Guid>(
                name: "ResourceId",
                schema: "operations",
                table: "BackgroundJobs",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true,
                oldComment: "關聯或稽核對象的業務資源識別碼。");

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerId",
                schema: "operations",
                table: "BackgroundJobs",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "LeaseUntil",
                schema: "operations",
                table: "BackgroundJobs",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true,
                oldComment: "背景工作租約的到期時間。");

            migrationBuilder.AlterColumn<Guid>(
                name: "LeaseToken",
                schema: "operations",
                table: "BackgroundJobs",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true,
                oldComment: "背景工作租約的 fencing token，防止過期 worker 提交。");

            migrationBuilder.AlterColumn<string>(
                name: "Label",
                schema: "operations",
                table: "BackgroundJobs",
                type: "nvarchar(180)",
                maxLength: 180,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(180)",
                oldMaxLength: 180,
                oldComment: "分類標籤或階段的顯示文字。");

            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                schema: "operations",
                table: "BackgroundJobs",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32,
                oldComment: "業務操作、資源或成本的種類。");

            migrationBuilder.AlterColumn<string>(
                name: "ErrorMessage",
                schema: "operations",
                table: "BackgroundJobs",
                type: "nvarchar(240)",
                maxLength: 240,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(240)",
                oldMaxLength: 240,
                oldNullable: true,
                oldComment: "經限制的錯誤說明。");

            migrationBuilder.AlterColumn<string>(
                name: "ErrorCode",
                schema: "operations",
                table: "BackgroundJobs",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(80)",
                oldMaxLength: 80,
                oldNullable: true,
                oldComment: "對外安全的錯誤代碼，不含密碼或完整例外。");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "operations",
                table: "BackgroundJobs",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "資料建立時間，採 UTC offset。");

            migrationBuilder.AlterColumn<int>(
                name: "CompletedUnits",
                schema: "operations",
                table: "BackgroundJobs",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldComment: "已完成的真實工作單位數。");

            migrationBuilder.AlterColumn<bool>(
                name: "CancelRequested",
                schema: "operations",
                table: "BackgroundJobs",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldComment: "是否收到取消要求；不表示工作已停止。");

            migrationBuilder.AlterColumn<int>(
                name: "Attempt",
                schema: "operations",
                table: "BackgroundJobs",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldComment: "背景工作執行／重試次數。");

            migrationBuilder.AlterColumn<string>(
                name: "ActiveKey",
                schema: "operations",
                table: "BackgroundJobs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true,
                oldComment: "仍在執行工作的唯一鍵，避免同一業務重複排程。");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "operations",
                table: "BackgroundJobs",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "資料的主鍵識別碼。");

            migrationBuilder.AlterColumn<string>(
                name: "Result",
                schema: "operations",
                table: "AuditEvents",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(80)",
                oldMaxLength: 80,
                oldNullable: true,
                oldComment: "稽核操作結果或失敗代碼。");

            migrationBuilder.AlterColumn<Guid>(
                name: "ResourceId",
                schema: "operations",
                table: "AuditEvents",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true,
                oldComment: "關聯或稽核對象的業務資源識別碼。");

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerId",
                schema: "operations",
                table: "AuditEvents",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。");

            migrationBuilder.AlterColumn<string>(
                name: "DetailsJson",
                schema: "operations",
                table: "AuditEvents",
                type: "nvarchar(max)",
                maxLength: 40000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldMaxLength: 40000,
                oldNullable: true,
                oldComment: "稽核前後狀態或操作範圍 JSON；不含密碼、hash、token 或對話內容。");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "At",
                schema: "operations",
                table: "AuditEvents",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "稽核事件發生時間。");

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                schema: "operations",
                table: "AuditEvents",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldComment: "稽核操作名稱。");

            migrationBuilder.AlterColumn<long>(
                name: "Id",
                schema: "operations",
                table: "AuditEvents",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldComment: "資料的主鍵識別碼。")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<long>(
                name: "Size",
                schema: "attachments",
                table: "Attachments",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldComment: "原始附件大小，以 bytes 計。");

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerId",
                schema: "attachments",
                table: "Attachments",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。");

            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                schema: "attachments",
                table: "Attachments",
                type: "nvarchar(180)",
                maxLength: 180,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(180)",
                oldMaxLength: 180,
                oldComment: "原始附件檔名，不作為伺服器儲存路徑。");

            migrationBuilder.AlterColumn<string>(
                name: "ExtractedText",
                schema: "attachments",
                table: "Attachments",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true,
                oldComment: "附件分析後的文字。");

            migrationBuilder.AlterColumn<byte[]>(
                name: "Data",
                schema: "attachments",
                table: "Attachments",
                type: "varbinary(max)",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)",
                oldComment: "原始附件二進位內容；不在 wwwroot 公開。");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "attachments",
                table: "Attachments",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "資料建立時間，採 UTC offset。");

            migrationBuilder.AlterColumn<string>(
                name: "ContentType",
                schema: "attachments",
                table: "Attachments",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(80)",
                oldMaxLength: 80,
                oldComment: "核准的 MIME 型別。");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "attachments",
                table: "Attachments",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "資料的主鍵識別碼。");

            migrationBuilder.AlterColumn<int>(
                name: "Version",
                schema: "content",
                table: "Artifacts",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldComment: "業務版本號，用於歷史或樂觀並行控制。");

            migrationBuilder.AlterColumn<Guid>(
                name: "SourceMessageId",
                schema: "content",
                table: "Artifacts",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true,
                oldComment: "此成果版本所引用的來源訊息識別碼。");

            migrationBuilder.AlterColumn<Guid>(
                name: "ProjectId",
                schema: "content",
                table: "Artifacts",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true,
                oldComment: "關聯專案的識別碼。");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "content",
                table: "Artifacts",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "資料的主鍵識別碼。");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                schema: "content",
                table: "ArtifactRevisions",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120,
                oldComment: "介面顯示標題。");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "content",
                table: "ArtifactRevisions",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "資料建立時間，採 UTC offset。");

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                schema: "content",
                table: "ArtifactRevisions",
                type: "nvarchar(max)",
                maxLength: 64000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldMaxLength: 64000,
                oldComment: "訊息、版本或生成的文字內容。");

            migrationBuilder.AlterColumn<Guid>(
                name: "AuthorId",
                schema: "content",
                table: "ArtifactRevisions",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "建立此版本的使用者識別碼。");

            migrationBuilder.AlterColumn<int>(
                name: "Version",
                schema: "content",
                table: "ArtifactRevisions",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldComment: "業務版本號，用於歷史或樂觀並行控制。");

            migrationBuilder.AlterColumn<Guid>(
                name: "ArtifactId",
                schema: "content",
                table: "ArtifactRevisions",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "關聯成果文件的識別碼。");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "GrantedAt",
                schema: "access",
                table: "AdministratorBootstraps",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldComment: "角色或資源授權建立時間。");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "access",
                table: "AdministratorBootstraps",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldComment: "關聯使用者的 Users 主鍵。");

            migrationBuilder.Sql("UPDATE [access].[RoleGroups] SET [Name] = N'基本工作\u53f0' WHERE [Id] = N'workspace' AND [Name] = N'基本工作區'");
        }
    }
}
