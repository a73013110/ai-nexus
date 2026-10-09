using System;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AiNexus.Features.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "administration");

            migrationBuilder.EnsureSchema(
                name: "artifacts");

            migrationBuilder.EnsureSchema(
                name: "attachments");

            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.EnsureSchema(
                name: "jobs");

            migrationBuilder.EnsureSchema(
                name: "knowledge");

            migrationBuilder.EnsureSchema(
                name: "conversations");

            migrationBuilder.EnsureSchema(
                name: "diagnostics");

            migrationBuilder.EnsureSchema(
                name: "quality");

            migrationBuilder.EnsureSchema(
                name: "accesscontrol");

            migrationBuilder.EnsureSchema(
                name: "inference");

            migrationBuilder.EnsureSchema(
                name: "billing");

            migrationBuilder.EnsureSchema(
                name: "notifications");

            migrationBuilder.EnsureSchema(
                name: "projects");

            migrationBuilder.EnsureSchema(
                name: "library");

            migrationBuilder.EnsureSchema(
                name: "repositories");

            migrationBuilder.EnsureSchema(
                name: "collaboration");

            migrationBuilder.EnsureSchema(
                name: "sharing");

            migrationBuilder.EnsureSchema(
                name: "integrations");

            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.EnsureSchema(
                name: "websearch");

            migrationBuilder.CreateTable(
                name: "AuditEvents",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "資料的主鍵識別碼。")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。"),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "身分測試時實際發起操作的管理者；空值表示與 OwnerId 相同。"),
                    TraceId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true, comment: "W3C 流程追蹤識別，僅由伺服器建立。"),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "持久作業識別，跨佇列與重試保持不變。"),
                    IssueCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true, comment: "伺服器產生的不透明問題查證代碼；每個問題個別識別。"),
                    Action = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, comment: "稽核操作名稱。"),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "稽核對象的業務資源識別碼。"),
                    Result = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true, comment: "稽核操作結果或失敗代碼。"),
                    DetailsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 40000, nullable: true, comment: "稽核前後狀態或操作範圍 JSON；不含密碼、hash、token 或對話內容。"),
                    At = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "稽核事件發生時間。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.Id);
                },
                comment: "操作與管理異動稽核；保存實際管理者及有效身分，不記錄密碼或私密內容。");

            migrationBuilder.CreateTable(
                name: "DiagnosticEvents",
                schema: "diagnostics",
                columns: table => new
                {
                    LogId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "不可重複的日誌識別，SQL 補送去重鍵。"),
                    At = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "診斷事件發生的 UTC 時間，時間與 LogId 為排序及游標分頁鍵。"),
                    Level = table.Column<int>(type: "int", nullable: false, comment: "Microsoft.Extensions.Logging 層級值：Trace=0 到 Critical=5。"),
                    Category = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false, comment: "診斷事件的受控 Category 欄位；由集中日誌政策限制大小與遮罩。"),
                    EventId = table.Column<int>(type: "int", nullable: false, comment: "穩定的事件分類識別碼，跨程式版本保持意義一致。"),
                    EventName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, comment: "穩定的事件名稱，供模組及流程查詢。"),
                    MessageTemplate = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false, comment: "結構化訊息模板，禁止串接內容與秘密。"),
                    PropertiesJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8192, nullable: false, comment: "白名單純量 metadata，大小及欄位數受限。"),
                    Service = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false, comment: "診斷事件的受控 Service 欄位；由集中日誌政策限制大小與遮罩。"),
                    Environment = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, comment: "診斷事件的受控 Environment 欄位；由集中日誌政策限制大小與遮罩。"),
                    Version = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false, comment: "應用程式 informational version，用於辨認發版。"),
                    Instance = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, comment: "診斷事件的受控 Instance 欄位；由集中日誌政策限制大小與遮罩。"),
                    IssueCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true, comment: "伺服器產生的不透明問題查證代碼；每個問題個別識別。"),
                    TraceId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true, comment: "W3C 流程追蹤識別，僅由伺服器建立。"),
                    SpanId = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true, comment: "診斷事件的受控 SpanId 欄位；由集中日誌政策限制大小與遮罩。"),
                    RequestId = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true, comment: "診斷事件的受控 RequestId 欄位；由集中日誌政策限制大小與遮罩。"),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "持久作業識別，跨佇列與重試保持不變。"),
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "關聯背景工作識別碼。"),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "關聯生成或評測執行的識別碼。"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "伺服器解析的受控使用者識別碼；不接受客戶端傳入。"),
                    Attempt = table.Column<int>(type: "int", nullable: true, comment: "背景工作執行／重試次數。"),
                    Method = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true, comment: "診斷事件的受控 Method 欄位；由集中日誌政策限制大小與遮罩。"),
                    Route = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: true, comment: "HTTP 路由模板，不含實際路徑值或查詢參數。"),
                    StatusCode = table.Column<int>(type: "int", nullable: true, comment: "診斷事件的受控 StatusCode 欄位；由集中日誌政策限制大小與遮罩。"),
                    DurationMs = table.Column<double>(type: "float", nullable: true, comment: "診斷事件的受控 DurationMs 欄位；由集中日誌政策限制大小與遮罩。"),
                    ExternalService = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true, comment: "診斷事件的受控 ExternalService 欄位；由集中日誌政策限制大小與遮罩。"),
                    ErrorCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true, comment: "對外安全的錯誤代碼，不含密碼或完整例外。"),
                    ExceptionType = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: true, comment: "診斷事件的受控 ExceptionType 欄位；由集中日誌政策限制大小與遮罩。"),
                    ExceptionDetail = table.Column<string>(type: "nvarchar(max)", maxLength: 12000, nullable: true, comment: "省略例外自由文字與路徑的型別、錯誤碼及堆疊。"),
                    UntrustedClient = table.Column<bool>(type: "bit", nullable: false, comment: "明確標示不可信用戶端回報。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiagnosticEvents", x => x.LogId)
                        .Annotation("SqlServer:Clustered", false);
                },
                comment: "共用診斷日誌；只保存受控且已遮罩的事件欄位，LogId 唯一用於補送去重。");

            migrationBuilder.CreateTable(
                name: "EmbeddingProfiles",
                schema: "knowledge",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false, comment: "資料的主鍵識別碼。")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, comment: "供應商、模型、維度及輸入／切段規則的唯一指紋。"),
                    Provider = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, comment: "模型或搜尋服務供應商識別碼。"),
                    Model = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false, comment: "建立此向量空間時的模型識別碼。"),
                    Dimensions = table.Column<int>(type: "int", nullable: false, comment: "向量維度，限已建立資料表的 allowlist。"),
                    InputFormat = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, comment: "查詢輸入格式 plain 或 qwen-query。"),
                    QueryInstruction = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false, comment: "qwen-query 的檢索任務指令快照。"),
                    Revision = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, comment: "外部來源或 repository 的固定版本識別。"),
                    ChunkerConfiguration = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false, comment: "切段器版本及 token／重疊參數快照；重建期間保留舊版本。"),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, comment: "業務執行狀態。"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "資料建立時間，採 UTC offset。"),
                    ActivatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "此 profile 完整覆蓋並切換為 active 的 UTC 時間。"),
                    RetiredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "此 profile 退役的 UTC 時間；作為保留期清理依據。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmbeddingProfiles", x => x.Id);
                },
                comment: "向量空間及切段規則的不可變快照；同時最多一個 active。");

            migrationBuilder.CreateTable(
                name: "Features",
                schema: "accesscontrol",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, comment: "資料的主鍵識別碼。"),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false, comment: "業務物件的顯示名稱。"),
                    Route = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false, comment: "功能入口的本站路由；API 授權仍由後端政策判定。"),
                    SortOrder = table.Column<int>(type: "int", nullable: false, comment: "介面顯示順序；數值越小越前。"),
                    Enabled = table.Column<bool>(type: "bit", nullable: false, comment: "是否啟用；停用不刪除歷史資料。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Features", x => x.Id);
                },
                comment: "模組註冊的功能入口、顯示名稱、路由及啟用狀態。");

            migrationBuilder.CreateTable(
                name: "ModelProfiles",
                schema: "inference",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false, comment: "資料的主鍵識別碼。"),
                    Provider = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, comment: "模型或搜尋服務供應商識別碼。"),
                    ProviderModelId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false, comment: "送往指定供應商的原生模型識別碼，與核准路由識別碼分開保存。"),
                    DisplayName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false, comment: "模型的介面顯示名稱。"),
                    ContextTokens = table.Column<int>(type: "int", nullable: false, comment: "模型上下文容量，以 tokens 計。"),
                    MaxOutputTokens = table.Column<int>(type: "int", nullable: false, comment: "模型核准的最大輸出 tokens。"),
                    SupportsStreaming = table.Column<bool>(type: "bit", nullable: false, comment: "模型是否支援串流輸出。"),
                    SupportsUsage = table.Column<bool>(type: "bit", nullable: false, comment: "模型是否會回報實際用量。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelProfiles", x => x.Id);
                },
                comment: "核准模型的能力、上下文與輸出限制。");

            migrationBuilder.CreateTable(
                name: "RoleGroups",
                schema: "accesscontrol",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, comment: "資料的主鍵識別碼。"),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false, comment: "業務物件的顯示名稱。"),
                    Enabled = table.Column<bool>(type: "bit", nullable: false, comment: "是否啟用；停用不刪除歷史資料。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleGroups", x => x.Id);
                },
                comment: "角色所加入的功能群組，集中管理功能及模型政策。");

            migrationBuilder.CreateTable(
                name: "Roles",
                schema: "accesscontrol",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, comment: "資料的主鍵識別碼。"),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false, comment: "業務物件的顯示名稱。"),
                    Enabled = table.Column<bool>(type: "bit", nullable: false, comment: "是否啟用；停用不刪除歷史資料。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                },
                comment: "可分派給使用者的角色；停用後不再提供有效授權。");

            migrationBuilder.CreateTable(
                name: "Users",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料的主鍵識別碼。"),
                    Sid = table.Column<string>(type: "nvarchar(184)", maxLength: 184, nullable: false, comment: "AD 的不可變 SID；尚未綁定 AD 的手動帳號使用 managed: 識別碼。"),
                    Account = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, comment: "登入身分顯示帳號；AD 連結後保存目錄提供的帳號。"),
                    DisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, comment: "使用者的介面顯示名稱。"),
                    Enabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true, comment: "是否啟用；停用不刪除歷史資料。"),
                    AttachmentLimitBytes = table.Column<long>(type: "bigint", nullable: true, comment: "管理者設定的個人容量上限 bytes；優先於群組，空值使用群組或預設 5 GB。"),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "登入身分刪除時間；保留關聯與歷史資料。"),
                    AdEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true, comment: "是否允許使用 AD／Windows 整合驗證登入。"),
                    LocalEnabled = table.Column<bool>(type: "bit", nullable: false, comment: "是否允許本地密碼登入；與 AD 驗證獨立。"),
                    AdAccount = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, comment: "預先配置的 AD 帳號正規化值；唯一、不含網域，驗證成功後以 SID 固定綁定。"),
                    LocalAccount = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, comment: "本地登入帳號正規化值；唯一且不區分大小寫。"),
                    PasswordHash = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true, comment: "本地密碼的 Argon2id PHC 雜湊，含版本、成本、隨機 salt 與衍生值；不可還原。"),
                    SecurityVersion = table.Column<int>(type: "int", nullable: false, comment: "登入政策或密碼變更時遞增，立即撤銷舊工作階段。"),
                    ProfileManaged = table.Column<bool>(type: "bit", nullable: false, comment: "姓名是否由管理者維護；開啟後 AD 目錄不覆寫姓名。"),
                    FailedLogins = table.Column<int>(type: "int", nullable: false, comment: "本地登入連續失敗次數，用於暫時鎖定。"),
                    LockedUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "本地帳號暫時鎖定的到期時間；空值表示未鎖定。"),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "使用者最近登入／活動時間，採 UTC offset。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.CheckConstraint("CK_Users_AttachmentLimitBytes", "[AttachmentLimitBytes] IS NULL OR [AttachmentLimitBytes] BETWEEN 0 AND 1000000000000000");
                },
                comment: "使用者身分、AD SID 綁定、可用登入方式與工作階段撤銷版本；不保存 AD 密碼。");

            migrationBuilder.CreateTable(
                name: "GroupModelPolicies",
                schema: "accesscontrol",
                columns: table => new
                {
                    GroupId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, comment: "關聯功能群組的識別碼。"),
                    AllowedModelsJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true, comment: "模型白名單 JSON；群組取聯集，空值授予全部、空陣列不授權；個人白名單再限縮。"),
                    DailyTokenLimitsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true, comment: "各模型每日輸入加輸出 token 上限 JSON；授權群組取最高值、留空不限，個人覆寫優先，UTC 午夜重設。"),
                    StoredAttachmentLimitBytes = table.Column<long>(type: "bigint", nullable: true, comment: "個人附件儲存上限，以 bytes 計；群組限制取最低值。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupModelPolicies", x => x.GroupId);
                    table.ForeignKey(
                        name: "FK_GroupModelPolicies_RoleGroups_GroupId",
                        column: x => x.GroupId,
                        principalSchema: "accesscontrol",
                        principalTable: "RoleGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "功能群組的模型白名單、各模型每日 token 及附件空間限制。");

            migrationBuilder.CreateTable(
                name: "RoleGroupFeatures",
                schema: "accesscontrol",
                columns: table => new
                {
                    GroupId = table.Column<string>(type: "nvarchar(64)", nullable: false, comment: "關聯功能群組的識別碼。"),
                    FeatureId = table.Column<string>(type: "nvarchar(64)", nullable: false, comment: "關聯功能的識別碼。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleGroupFeatures", x => new { x.GroupId, x.FeatureId });
                    table.ForeignKey(
                        name: "FK_RoleGroupFeatures_Features_FeatureId",
                        column: x => x.FeatureId,
                        principalSchema: "accesscontrol",
                        principalTable: "Features",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoleGroupFeatures_RoleGroups_GroupId",
                        column: x => x.GroupId,
                        principalSchema: "accesscontrol",
                        principalTable: "RoleGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "功能群組與功能的授權關聯；有效功能取聯集。");

            migrationBuilder.CreateTable(
                name: "RoleGroupRoles",
                schema: "accesscontrol",
                columns: table => new
                {
                    RoleId = table.Column<string>(type: "nvarchar(64)", nullable: false, comment: "關聯角色的識別碼。"),
                    GroupId = table.Column<string>(type: "nvarchar(64)", nullable: false, comment: "關聯功能群組的識別碼。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleGroupRoles", x => new { x.RoleId, x.GroupId });
                    table.ForeignKey(
                        name: "FK_RoleGroupRoles_RoleGroups_GroupId",
                        column: x => x.GroupId,
                        principalSchema: "accesscontrol",
                        principalTable: "RoleGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoleGroupRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "accesscontrol",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "角色與功能群組的授權關聯。");

            migrationBuilder.CreateTable(
                name: "AdministratorBootstraps",
                schema: "administration",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "關聯使用者的 Users 主鍵。"),
                    GrantedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "角色或資源授權建立時間。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdministratorBootstraps", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_AdministratorBootstraps_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "管理者首次啟動授權的永久標記，防止撤銷後重新授權。");

            migrationBuilder.CreateTable(
                name: "Attachments",
                schema: "attachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料的主鍵識別碼。"),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。"),
                    FileName = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false, comment: "原始附件檔名，不作為伺服器儲存路徑。"),
                    ContentType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false, comment: "核准的 MIME 型別。"),
                    Size = table.Column<long>(type: "bigint", nullable: false, comment: "原始附件大小，以 bytes 計。"),
                    StorageKey = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, comment: "站外原檔的不可變隨機識別碼；不含使用者路徑或檔名。"),
                    StorageState = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, comment: "原檔儲存狀態 pending／ready／deleting；刪檔成功才釋放 metadata 與容量。"),
                    ExtractedText = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "附件分析後的文字。"),
                    InLibrary = table.Column<bool>(type: "bit", nullable: false, comment: "是否由個人檔案庫獨立保留原檔；移除對話或知識索引不會刪除保留的檔案。"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "資料建立時間，採 UTC offset。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attachments", x => x.Id);
                    table.CheckConstraint("CK_Attachments_Size", "[Size] > 0");
                    table.CheckConstraint("CK_Attachments_StorageState", "[StorageState] IN ('pending', 'ready', 'deleting')");
                    table.ForeignKey(
                        name: "FK_Attachments_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "站外附件原檔的 metadata、儲存識別、擷取文字及生命週期；不保存原始 bytes。");

            migrationBuilder.CreateTable(
                name: "ModelInvocations",
                schema: "inference",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料的主鍵識別碼。"),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。"),
                    Kind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, comment: "模型呼叫種類。"),
                    ModelId = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false, comment: "核准模型的內部識別碼。"),
                    Provider = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, comment: "模型或搜尋服務供應商識別碼。"),
                    DurationMilliseconds = table.Column<long>(type: "bigint", nullable: true, comment: "從請求建立至終止的總耗時毫秒；包括排隊、生成、取消與失敗。"),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, comment: "業務執行狀態。"),
                    ReservedTokens = table.Column<long>(type: "bigint", nullable: false, comment: "生成預留的保守輸入加最大輸出 token；執行中或缺失 usage 時占用配額，未執行即取消釋放。"),
                    InputTokens = table.Column<long>(type: "bigint", nullable: true, comment: "模型回報的輸入 tokens；未知保持空值。"),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: true, comment: "模型回報的輸出 tokens；未知保持空值。"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "資料建立時間，採 UTC offset。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelInvocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelInvocations_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "文字、OCR、embedding 等模型呼叫的狀態與實際用量。");

            migrationBuilder.CreateTable(
                name: "ModelPrices",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料的主鍵識別碼。"),
                    Provider = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, comment: "模型或搜尋服務供應商識別碼。"),
                    ModelId = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false, comment: "核准模型的內部識別碼。"),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false, comment: "費用幣別代碼；不同幣別不可直接合計。"),
                    Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, comment: "成本類型。"),
                    InputPerMillion = table.Column<decimal>(type: "decimal(20,8)", precision: 20, scale: 8, nullable: false, comment: "每百萬輸入 tokens 的單價。"),
                    CachedInputPerMillion = table.Column<decimal>(type: "decimal(20,8)", precision: 20, scale: 8, nullable: false, comment: "每百萬快取輸入 tokens 的單價。"),
                    OutputPerMillion = table.Column<decimal>(type: "decimal(20,8)", precision: 20, scale: 8, nullable: false, comment: "每百萬輸出 tokens 的單價。"),
                    PerRequest = table.Column<decimal>(type: "decimal(20,8)", precision: 20, scale: 8, nullable: false, comment: "每次呼叫的固定單價。"),
                    RequestCharge = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, comment: "搜尋服務每次請求的費用。"),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false, comment: "使用者提供的補充說明。"),
                    EffectiveAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "此價格版本開始生效的時間。"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "資料建立時間，採 UTC offset。"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "建立紀錄的使用者識別碼。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelPrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelPrices_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "依供應商、模型、幣別及成本類型保存的不可變價格版本。");

            migrationBuilder.CreateTable(
                name: "Notifications",
                schema: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料的主鍵識別碼。"),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。"),
                    Version = table.Column<int>(type: "int", nullable: false, comment: "業務版本號，用於歷史或樂觀並行控制。"),
                    EventKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false, comment: "通知來源事件的冪等識別碼。"),
                    Type = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false, comment: "事件的種類。"),
                    Severity = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, comment: "通知呈現層級：info、success 或 error。"),
                    IssueCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true, comment: "伺服器產生的不透明問題查證代碼；每個問題個別識別。"),
                    Title = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false, comment: "介面顯示標題。"),
                    Body = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false, comment: "通知摘要，不包含完整私密原文。"),
                    TargetKind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, comment: "已核准的功能導向類型。"),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "通知所指向的業務識別碼。"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "資料建立時間，採 UTC offset。"),
                    ReadAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "通知已閱讀的時間；空值代表未讀。"),
                    DismissedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "通知移除的時間；空值代表仍可查看。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "使用者個人通知、版本化導向、閱讀狀態與事件去重鍵。");

            migrationBuilder.CreateTable(
                name: "PromptTemplates",
                schema: "library",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料的主鍵識別碼。"),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。"),
                    Title = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false, comment: "介面顯示標題。"),
                    Content = table.Column<string>(type: "nvarchar(max)", maxLength: 12000, nullable: false, comment: "提示詞內容。"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "資料最後修改時間，採 UTC offset。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromptTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PromptTemplates_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "使用者私人提示詞範本。");

            migrationBuilder.CreateTable(
                name: "RepositoryConnections",
                schema: "repositories",
                columns: table => new
                {
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。"),
                    BaseUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false, comment: "Gitea 連線主機位址。"),
                    Login = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, comment: "外部服務的使用者登入名稱。"),
                    ProtectedToken = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: false, comment: "以 ASP.NET Data Protection 保護的外部 token；不可在 API、稽核或日誌回傳。"),
                    ConnectedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "使用者建立外部服務連線的時間。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositoryConnections", x => x.OwnerId);
                    table.ForeignKey(
                        name: "FK_RepositoryConnections_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "使用者個人的 Gitea 連線及 Data Protection 保護的存取 token。");

            migrationBuilder.CreateTable(
                name: "Resources",
                schema: "collaboration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料的主鍵識別碼。"),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。"),
                    Kind = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false, comment: "資源種類。"),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false, comment: "業務物件的顯示名稱。"),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "父資源識別碼；子資源繼承父資源的 ACL。"),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, comment: "是否邏輯刪除；不自動刪除歷史紀錄。"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "資料建立時間，採 UTC offset。"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "資料最後修改時間，採 UTC offset。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Resources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Resources_Resources_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "collaboration",
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Resources_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "共用資源的擁有者、種類、階層及版本；作為資料 ACL 邊界。");

            migrationBuilder.CreateTable(
                name: "UserModelPolicies",
                schema: "accesscontrol",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "關聯使用者的 Users 主鍵。"),
                    AllowedModelsJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true, comment: "模型白名單 JSON；群組取聯集，空值授予全部、空陣列不授權；個人白名單再限縮。"),
                    DailyTokenLimitsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true, comment: "各模型每日輸入加輸出 token 上限 JSON；授權群組取最高值、留空不限，個人覆寫優先，UTC 午夜重設。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserModelPolicies", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserModelPolicies_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "使用者的模型白名單與各模型每日 token 覆寫政策。");

            migrationBuilder.CreateTable(
                name: "UserPreferences",
                schema: "identity",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "偏好所屬使用者的 Users 主鍵。"),
                    Theme = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false, comment: "外觀偏好：system、light 或 dark。"),
                    ReducedMotion = table.Column<bool>(type: "bit", nullable: false, comment: "是否減少動畫與動態效果。"),
                    DefaultModelId = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true, comment: "偏好的核准模型識別碼；空值使用伺服器預設。"),
                    ReadingFontSize = table.Column<int>(type: "int", nullable: false, defaultValue: 15, comment: "對話文字大小，以 CSS px 的偏好值記錄。"),
                    ReadingLineHeight = table.Column<double>(type: "float", nullable: false, defaultValue: 1.2, comment: "對話閱讀行高倍率。"),
                    Density = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, defaultValue: "comfortable", comment: "介面密度偏好。"),
                    SidebarWidth = table.Column<int>(type: "int", nullable: false, defaultValue: 240, comment: "側欄寬度偏好。"),
                    ReadingWidth = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, defaultValue: "standard", comment: "閱讀區寬度偏好。"),
                    EnterToSend = table.Column<bool>(type: "bit", nullable: false, defaultValue: true, comment: "是否以 Enter 送出提問；IME 組字不送出。"),
                    AutoFollow = table.Column<bool>(type: "bit", nullable: false, defaultValue: true, comment: "生成時是否跟隨最新回答。"),
                    SaveLocalDrafts = table.Column<bool>(type: "bit", nullable: false, defaultValue: true, comment: "是否在瀏覽器按使用者保存草稿。"),
                    NotifyOnCompletion = table.Column<bool>(type: "bit", nullable: false, comment: "是否在背景分頁提醒回答完成。"),
                    DefaultReasoningEffort = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, defaultValue: "auto", comment: "偏好的推理強度。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPreferences", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserPreferences_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "使用者個人外觀、閱讀、對話操作與通知偏好；不含服務密鑰。");

            migrationBuilder.CreateTable(
                name: "UserRoles",
                schema: "accesscontrol",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "關聯使用者的 Users 主鍵。"),
                    RoleId = table.Column<string>(type: "nvarchar(64)", nullable: false, comment: "關聯角色的識別碼。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "accesscontrol",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "使用者與角色的分派關聯。");

            migrationBuilder.CreateTable(
                name: "WebSearches",
                schema: "websearch",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料的主鍵識別碼。"),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。"),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "關聯對話的識別碼。"),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false, comment: "擁有者範圍內的冪等請求識別，避免重試重複處理。"),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, comment: "請求內容指紋，用於辨識冪等識別碼衝突。"),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, comment: "業務執行狀態。"),
                    ResultsJson = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "搜尋結果的 JSON 快照。"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "資料建立時間，採 UTC offset。"),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "觸發搜尋的 GenerationRuns 識別碼。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebSearches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WebSearches_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "使用者明確啟用的網路搜尋、冪等識別、結果與費用。");

            migrationBuilder.CreateTable(
                name: "BackgroundJobs",
                schema: "jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料的主鍵識別碼。"),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。"),
                    TraceId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true, comment: "W3C 流程追蹤識別，僅由伺服器建立。"),
                    ParentSpanId = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true, comment: "排程來源的 W3C span 識別，重試沿用同一 trace。"),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "持久作業識別，跨佇列與重試保持不變。"),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "工作處理的業務資源識別碼。"),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "操作所關聯的業務對象識別碼。"),
                    Kind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, comment: "背景工作種類。"),
                    Label = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false, comment: "分類標籤或階段的顯示文字。"),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, comment: "業務執行狀態。"),
                    Stage = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false, comment: "背景工作目前階段。"),
                    ActiveKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true, comment: "仍在執行工作的唯一鍵，避免同一業務重複排程。"),
                    LeaseToken = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "背景工作租約的 fencing token，防止過期 worker 提交。"),
                    LeaseUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "背景工作租約的到期時間。"),
                    CancelRequested = table.Column<bool>(type: "bit", nullable: false, comment: "是否收到取消要求；不表示工作已停止。"),
                    Attempt = table.Column<int>(type: "int", nullable: false, comment: "背景工作執行／重試次數。"),
                    CompletedUnits = table.Column<int>(type: "int", nullable: false, comment: "已完成的真實工作單位數。"),
                    TotalUnits = table.Column<int>(type: "int", nullable: true, comment: "已知的總工作單位數；未知不表示百分比。"),
                    IssueCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true, comment: "伺服器產生的不透明問題查證代碼；每個問題個別識別。"),
                    ErrorCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true, comment: "對外安全的錯誤代碼，不含密碼或完整例外。"),
                    ErrorMessage = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: true, comment: "固定安全提示與查證代碼；不可保存例外自由文字。"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "資料建立時間，採 UTC offset。"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "資料最後修改時間，採 UTC offset。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackgroundJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BackgroundJobs_Resources_ResourceId",
                        column: x => x.ResourceId,
                        principalSchema: "collaboration",
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BackgroundJobs_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "文件索引與評測等背景工作的租約、進度、重試與取消狀態。");

            migrationBuilder.CreateTable(
                name: "Collections",
                schema: "knowledge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料的主鍵識別碼。"),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false, comment: "業務物件的用途說明。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Collections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Collections_Resources_Id",
                        column: x => x.Id,
                        principalSchema: "collaboration",
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "知識庫的資料資源關聯及索引資訊。");

            migrationBuilder.CreateTable(
                name: "EvaluationSets",
                schema: "quality",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料的主鍵識別碼。"),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false, comment: "業務物件的用途說明。"),
                    CasesJson = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "固定評測案例的 JSON 快照。"),
                    Version = table.Column<int>(type: "int", nullable: false, comment: "業務版本號，用於歷史或樂觀並行控制。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationSets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvaluationSets_Resources_Id",
                        column: x => x.Id,
                        principalSchema: "collaboration",
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "評測題庫、固定測試案例與版本。");

            migrationBuilder.CreateTable(
                name: "Projects",
                schema: "projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料的主鍵識別碼。"),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false, comment: "業務物件的用途說明。"),
                    Instructions = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false, comment: "專案共用指令。"),
                    Version = table.Column<int>(type: "int", nullable: false, comment: "業務版本號，用於歷史或樂觀並行控制。"),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false, comment: "是否封存對話；封存後不再接受新的生成。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Projects_Resources_Id",
                        column: x => x.Id,
                        principalSchema: "collaboration",
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "專案資源、共用指令及範本版本。");

            migrationBuilder.CreateTable(
                name: "ResourceAttachments",
                schema: "attachments",
                columns: table => new
                {
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "關聯 Resources 的識別碼。"),
                    AttachmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "引用的附件識別碼。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceAttachments", x => new { x.ResourceId, x.AttachmentId });
                    table.ForeignKey(
                        name: "FK_ResourceAttachments_Attachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalSchema: "attachments",
                        principalTable: "Attachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResourceAttachments_Resources_ResourceId",
                        column: x => x.ResourceId,
                        principalSchema: "collaboration",
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "知識庫或專案資源與附件的引用關聯。");

            migrationBuilder.CreateTable(
                name: "ResourceGroups",
                schema: "collaboration",
                columns: table => new
                {
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "關聯 Resources 的識別碼。"),
                    GroupId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, comment: "關聯功能群組的識別碼。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceGroups", x => new { x.ResourceId, x.GroupId });
                    table.ForeignKey(
                        name: "FK_ResourceGroups_Resources_ResourceId",
                        column: x => x.ResourceId,
                        principalSchema: "collaboration",
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ResourceGroups_RoleGroups_GroupId",
                        column: x => x.GroupId,
                        principalSchema: "accesscontrol",
                        principalTable: "RoleGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "資源對功能群組授予的唯讀權限。");

            migrationBuilder.CreateTable(
                name: "ResourceMembers",
                schema: "collaboration",
                columns: table => new
                {
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "關聯 Resources 的識別碼。"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "關聯使用者的 Users 主鍵。"),
                    Role = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false, comment: "具名成員的權限：viewer 或 editor。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceMembers", x => new { x.ResourceId, x.UserId });
                    table.ForeignKey(
                        name: "FK_ResourceMembers_Resources_ResourceId",
                        column: x => x.ResourceId,
                        principalSchema: "collaboration",
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ResourceMembers_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "資源對具名使用者授予的閱讀或編輯權限。");

            migrationBuilder.CreateTable(
                name: "ShareLinks",
                schema: "sharing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料的主鍵識別碼。"),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。"),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "外部資料來源識別碼。"),
                    Kind = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false, comment: "分享內容的種類。"),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false, comment: "介面顯示標題。"),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "分享時的固定內容快照；不隨後續編輯變動。"),
                    IncludeAttachments = table.Column<bool>(type: "bit", nullable: false, comment: "是否明確允許分享附件。"),
                    IsRevoked = table.Column<bool>(type: "bit", nullable: false, comment: "分享是否已撤銷。"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "資料建立時間，採 UTC offset。"),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "分享到期時間。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShareLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShareLinks_Resources_Id",
                        column: x => x.Id,
                        principalSchema: "collaboration",
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShareLinks_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "分享版本快照、有效期限、附件選項及撤銷狀態。");

            migrationBuilder.CreateTable(
                name: "RepositoryReviews",
                schema: "repositories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料的主鍵識別碼。"),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。"),
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "關聯背景工作識別碼。"),
                    BaseUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false, comment: "Gitea 連線主機位址。"),
                    Repository = table.Column<string>(type: "nvarchar(201)", maxLength: 201, nullable: false, comment: "Gitea repository 的 owner/name 識別。"),
                    Commit = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, comment: "匯入當時固定的 commit SHA。"),
                    BaseCommit = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, comment: "區間 review 的起點 commit SHA；空值表示單一 commit。"),
                    ModelId = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false, comment: "核准模型的內部識別碼。"),
                    ConfigurationFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, comment: "固定模型與生成設定的 SHA-256 指紋。"),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false, comment: "使用者提供的補充說明。"),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false, comment: "擁有者範圍內的冪等請求識別，避免重試重複處理。"),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, comment: "請求內容指紋，用於辨識冪等識別碼衝突。"),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: 1000000, nullable: false, comment: "分享時的固定內容快照；不隨後續編輯變動。"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "資料建立時間，採 UTC offset。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositoryReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepositoryReviews_BackgroundJobs_JobId",
                        column: x => x.JobId,
                        principalSchema: "jobs",
                        principalTable: "BackgroundJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RepositoryReviews_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "固定 commit 或區間 diff 的私人 review、模型設定指紋與背景任務。");

            migrationBuilder.CreateTable(
                name: "RetrievalEvaluations",
                schema: "quality",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料的主鍵識別碼。"),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。"),
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "關聯背景工作識別碼。"),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false, comment: "介面顯示標題。"),
                    CollectionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "評測所使用的知識庫識別碼陣列；執行與讀取時重新檢查授權。"),
                    CasesJson = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "固定評測案例的 JSON 快照。"),
                    ConfigurationFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, comment: "固定模型與生成設定的 SHA-256 指紋。"),
                    ProfileKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, comment: "評測所固定的向量空間及切段規則識別。"),
                    TopK = table.Column<int>(type: "int", nullable: false, comment: "評測所固定的最大檢索結果數。"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "資料建立時間，採 UTC offset。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetrievalEvaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RetrievalEvaluations_BackgroundJobs_JobId",
                        column: x => x.JobId,
                        principalSchema: "jobs",
                        principalTable: "BackgroundJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RetrievalEvaluations_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "檢索驗收集、授權知識庫、索引與設定指紋及可續跑背景工作；不保存檢索來源原文。");

            migrationBuilder.CreateTable(
                name: "Documents",
                schema: "knowledge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料的主鍵識別碼。"),
                    CollectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "關聯知識庫的識別碼。"),
                    AttachmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "引用的附件識別碼。"),
                    FileName = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false, comment: "原始附件檔名，不作為伺服器儲存路徑。"),
                    ContentType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false, comment: "核准的 MIME 型別。"),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, comment: "業務執行狀態。"),
                    PageCount = table.Column<int>(type: "int", nullable: false, comment: "文件總頁數。"),
                    ChunkCount = table.Column<int>(type: "int", nullable: false, comment: "文件已建立的檢索片段數。"),
                    Warning = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true, comment: "處理過程中的非致命提示。"),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, comment: "是否邏輯刪除；不自動刪除歷史紀錄。"),
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "關聯背景工作識別碼。"),
                    TextContent = table.Column<string>(type: "nvarchar(max)", maxLength: 200000, nullable: true, comment: "純文字來源的可編輯內容；一般上傳原檔保持空值。"),
                    TextVersion = table.Column<int>(type: "int", nullable: false, comment: "純文字內容的樂觀並行版本號。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documents_Attachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalSchema: "attachments",
                        principalTable: "Attachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Documents_Collections_CollectionId",
                        column: x => x.CollectionId,
                        principalSchema: "knowledge",
                        principalTable: "Collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Documents_Resources_Id",
                        column: x => x.Id,
                        principalSchema: "collaboration",
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "知識庫文件的原始附件、分析／索引狀態與 embedding profile。");

            migrationBuilder.CreateTable(
                name: "EvaluationRuns",
                schema: "quality",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料的主鍵識別碼。"),
                    SetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "評測題庫識別碼。"),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。"),
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "關聯背景工作識別碼。"),
                    SetVersion = table.Column<int>(type: "int", nullable: false, comment: "評測執行當時的題庫版本。"),
                    SetTitle = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false, comment: "評測執行當時的題庫名稱快照。"),
                    CasesJson = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "固定評測案例的 JSON 快照。"),
                    VariantsJson = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "評測模型／參數組合的 JSON 快照。"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "資料建立時間，採 UTC offset。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvaluationRuns_BackgroundJobs_JobId",
                        column: x => x.JobId,
                        principalSchema: "jobs",
                        principalTable: "BackgroundJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvaluationRuns_EvaluationSets_SetId",
                        column: x => x.SetId,
                        principalSchema: "quality",
                        principalTable: "EvaluationSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvaluationRuns_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "評測執行的題庫與模型設定快照、背景工作與狀態。");

            migrationBuilder.CreateTable(
                name: "Conversations",
                schema: "conversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料的主鍵識別碼。"),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。"),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "關聯專案的識別碼。"),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false, comment: "介面顯示標題。"),
                    ActiveLeafId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "對話目前顯示分支的最後訊息識別碼。"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "資料建立時間，採 UTC offset。"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "資料最後修改時間，採 UTC offset。"),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, comment: "是否邏輯刪除；不自動刪除歷史紀錄。"),
                    IsFavorite = table.Column<bool>(type: "bit", nullable: false, comment: "是否標記收藏。"),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false, comment: "是否封存對話；封存後不再接受新的生成。"),
                    SystemInstruction = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false, comment: "對話專用的回答指令。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Conversations_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "projects",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Conversations_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "使用者私人對話、目前訊息分支、收藏封存與自訂指令。");

            migrationBuilder.CreateTable(
                name: "ProjectTemplates",
                schema: "projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料的主鍵識別碼。"),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "關聯專案的識別碼。"),
                    Title = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false, comment: "介面顯示標題。"),
                    Content = table.Column<string>(type: "nvarchar(max)", maxLength: 12000, nullable: false, comment: "範本的開場提示內容。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectTemplates_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "projects",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "專案建立範本與預設指令。");

            migrationBuilder.CreateTable(
                name: "ShareRecipients",
                schema: "sharing",
                columns: table => new
                {
                    ShareId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "關聯分享的識別碼。"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "關聯使用者的 Users 主鍵。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShareRecipients", x => new { x.ShareId, x.UserId });
                    table.ForeignKey(
                        name: "FK_ShareRecipients_ShareLinks_ShareId",
                        column: x => x.ShareId,
                        principalSchema: "sharing",
                        principalTable: "ShareLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShareRecipients_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "分享的具名收件人；與原資源 ACL 分開判定。");

            migrationBuilder.CreateTable(
                name: "RepositoryReviewResults",
                schema: "repositories",
                columns: table => new
                {
                    ReviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "關聯私人程式碼 review 的識別碼。"),
                    Ordinal = table.Column<int>(type: "int", nullable: false, comment: "同一父物件內的呈現順序。"),
                    Output = table.Column<string>(type: "nvarchar(max)", maxLength: 64000, nullable: false, comment: "評測模型的實際回答。"),
                    Truncated = table.Column<bool>(type: "bit", nullable: false, comment: "評測輸出是否因上限截斷。"),
                    InputTokens = table.Column<long>(type: "bigint", nullable: true, comment: "模型回報的輸入 tokens；未知保持空值。"),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: true, comment: "模型回報的輸出 tokens；未知保持空值。"),
                    ElapsedMs = table.Column<long>(type: "bigint", nullable: false, comment: "執行耗時，以毫秒計。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositoryReviewResults", x => new { x.ReviewId, x.Ordinal });
                    table.ForeignKey(
                        name: "FK_RepositoryReviewResults_RepositoryReviews_ReviewId",
                        column: x => x.ReviewId,
                        principalSchema: "repositories",
                        principalTable: "RepositoryReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "Review 各區段的持久結果及用量；重試沿用已完成區段。");

            migrationBuilder.CreateTable(
                name: "RetrievalEvaluationResults",
                schema: "quality",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "關聯 RetrievalEvaluations 的識別碼。"),
                    CaseIndex = table.Column<int>(type: "int", nullable: false, comment: "評測案例的從零開始索引。"),
                    Mode = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false, comment: "請求的檢索比較模式。"),
                    ActualMode = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false, comment: "實際檢索模式，包含略過或降級標記。"),
                    Unavailable = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true, comment: "此模式無法評測的錯誤代碼；空值表示已完成。"),
                    Recall = table.Column<double>(type: "float", nullable: true, comment: "前 K 筆命中的相關文件比例；無答案題保持空值。"),
                    ReciprocalRank = table.Column<double>(type: "float", nullable: true, comment: "第一筆相關命中的排名倒數；無答案題保持空值。"),
                    Ndcg = table.Column<double>(type: "float", nullable: true, comment: "前 K 筆依相關性分級計算的正規化折損累積增益。"),
                    Refused = table.Column<bool>(type: "bit", nullable: true, comment: "無答案題是否未提供來源；有答案題保持空值。"),
                    RewriteMs = table.Column<long>(type: "bigint", nullable: false, comment: "查詢改寫耗時，單位毫秒。"),
                    EmbedMs = table.Column<long>(type: "bigint", nullable: false, comment: "查詢向量化含快取耗時，單位毫秒。"),
                    SearchMs = table.Column<long>(type: "bigint", nullable: false, comment: "授權候選召回耗時，單位毫秒。"),
                    RerankMs = table.Column<long>(type: "bigint", nullable: false, comment: "重排耗時，單位毫秒。"),
                    ElapsedMs = table.Column<long>(type: "bigint", nullable: false, comment: "執行耗時，以毫秒計。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetrievalEvaluationResults", x => new { x.RunId, x.CaseIndex, x.Mode });
                    table.ForeignKey(
                        name: "FK_RetrievalEvaluationResults_RetrievalEvaluations_RunId",
                        column: x => x.RunId,
                        principalSchema: "quality",
                        principalTable: "RetrievalEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "四種檢索模式的相關性、無來源拒答與延遲指標；不保存查詢或檢索來源原文。");

            migrationBuilder.CreateTable(
                name: "Chunks",
                schema: "knowledge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料的主鍵識別碼。"),
                    SearchId = table.Column<int>(type: "int", nullable: false, comment: "全文索引使用的整數唯一鍵；保留未來 ANN 映射。")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "關聯知識文件的識別碼。"),
                    StartPage = table.Column<int>(type: "int", nullable: false, comment: "片段開始的原始文件頁碼。"),
                    EndPage = table.Column<int>(type: "int", nullable: false, comment: "片段結束的原始文件頁碼。"),
                    Ordinal = table.Column<int>(type: "int", nullable: false, comment: "同一父物件內的呈現順序。"),
                    HeadingPath = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false, comment: "由標題階層組成的結構路徑。"),
                    Text = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false, comment: "文件頁面／片段的擷取文字。"),
                    ContentHash = table.Column<byte[]>(type: "binary(32)", nullable: false, comment: "實際向量輸入（文件名稱、標題路徑與本文）的 SHA-256。"),
                    TokenEstimate = table.Column<int>(type: "int", nullable: false, comment: "依 CJK 與其他字元比例估算的片段 token 數。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Chunks_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "knowledge",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "結構化檢索片段、頁碼與內容指紋；查詢先套用資料 ACL。");

            migrationBuilder.CreateTable(
                name: "DocumentPages",
                schema: "knowledge",
                columns: table => new
                {
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "關聯知識文件的識別碼。"),
                    PageNumber = table.Column<int>(type: "int", nullable: false, comment: "文件頁碼，從 1 開始。"),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "文件頁面／片段的擷取文字。"),
                    Extraction = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, comment: "附件文字擷取方法或結果。"),
                    NeedsReview = table.Column<bool>(type: "bit", nullable: false, comment: "此評測結果是否需要人工覆核。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentPages", x => new { x.DocumentId, x.PageNumber });
                    table.ForeignKey(
                        name: "FK_DocumentPages_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "knowledge",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "文件逐頁擷取的文字、頁碼與 OCR 結果。");

            migrationBuilder.CreateTable(
                name: "RepositoryImports",
                schema: "repositories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料的主鍵識別碼。"),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。"),
                    CollectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "關聯知識庫的識別碼。"),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "關聯知識文件的識別碼。"),
                    Repository = table.Column<string>(type: "nvarchar(201)", maxLength: 201, nullable: false, comment: "Gitea repository 的 owner/name 識別。"),
                    Path = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false, comment: "repository 內的檔案路徑，不是伺服器路徑。"),
                    Commit = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, comment: "匯入當時固定的 commit SHA。"),
                    BaseUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false, comment: "Gitea 連線主機位址。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositoryImports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepositoryImports_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "knowledge",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "程式庫文件匯入所固定的主機、repository、commit 與檔案路徑。");

            migrationBuilder.CreateTable(
                name: "EvaluationResults",
                schema: "quality",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "關聯 EvaluationRuns 的識別碼。"),
                    CaseIndex = table.Column<int>(type: "int", nullable: false, comment: "評測案例的從零開始索引。"),
                    VariantIndex = table.Column<int>(type: "int", nullable: false, comment: "評測模型／參數組合的從零開始索引。"),
                    Output = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "評測模型的實際回答。"),
                    Truncated = table.Column<bool>(type: "bit", nullable: false, comment: "評測輸出是否因上限截斷。"),
                    RequiredMatches = table.Column<int>(type: "int", nullable: false, comment: "命中的必要條件數。"),
                    RequiredTotal = table.Column<int>(type: "int", nullable: false, comment: "必要條件總數。"),
                    ForbiddenMatches = table.Column<int>(type: "int", nullable: false, comment: "命中的禁止條件數。"),
                    ElapsedMs = table.Column<long>(type: "bigint", nullable: false, comment: "執行耗時，以毫秒計。"),
                    InputTokens = table.Column<long>(type: "bigint", nullable: true, comment: "模型回報的輸入 tokens；未知保持空值。"),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: true, comment: "模型回報的輸出 tokens；未知保持空值。"),
                    ReviewScore = table.Column<int>(type: "int", nullable: true, comment: "人工覆核分數；未覆核保持空值。"),
                    ReviewNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false, comment: "人工覆核意見。"),
                    ReviewerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "人工覆核者的使用者識別碼。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationResults", x => new { x.RunId, x.CaseIndex, x.VariantIndex });
                    table.ForeignKey(
                        name: "FK_EvaluationResults_EvaluationRuns_RunId",
                        column: x => x.RunId,
                        principalSchema: "quality",
                        principalTable: "EvaluationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EvaluationResults_Users_ReviewerId",
                        column: x => x.ReviewerId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "各案例／模型組合的輸出、指標、用量與人工覆核結果。");

            migrationBuilder.CreateTable(
                name: "ConversationCollections",
                schema: "knowledge",
                columns: table => new
                {
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "關聯對話的識別碼。"),
                    CollectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "關聯知識庫的識別碼。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationCollections", x => new { x.ConversationId, x.CollectionId });
                    table.ForeignKey(
                        name: "FK_ConversationCollections_Collections_CollectionId",
                        column: x => x.CollectionId,
                        principalSchema: "knowledge",
                        principalTable: "Collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationCollections_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "conversations",
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "對話選定的知識庫來源關聯。");

            migrationBuilder.CreateTable(
                name: "ConversationLabels",
                schema: "conversations",
                columns: table => new
                {
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "關聯對話的識別碼。"),
                    Name = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false, comment: "業務物件的顯示名稱。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationLabels", x => new { x.ConversationId, x.Name });
                    table.ForeignKey(
                        name: "FK_ConversationLabels_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "conversations",
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "使用者對話的分類標籤。");

            migrationBuilder.CreateTable(
                name: "Messages",
                schema: "conversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料的主鍵識別碼。"),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "關聯對話的識別碼。"),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "父訊息識別碼；編輯與重新生成形成分支樹。"),
                    Role = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, comment: "訊息角色：user 或 assistant。"),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "訊息文字內容。"),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, comment: "業務執行狀態。"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "資料建立時間，採 UTC offset。"),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "產生此訊息的 GenerationRuns 識別碼。"),
                    ModelId = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true, comment: "核准模型的內部識別碼。"),
                    IssueCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true, comment: "伺服器產生的不透明問題查證代碼；每個問題個別識別。"),
                    ErrorCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true, comment: "對外安全的錯誤代碼，不含密碼或完整例外。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Messages_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "conversations",
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Messages_Messages_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "conversations",
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "對話訊息樹；提問、回答、重新生成與編輯保留各版本。");

            migrationBuilder.CreateTable(
                name: "ModelCharges",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料的主鍵識別碼。"),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。"),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "關聯對話的識別碼。"),
                    Provider = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, comment: "模型或搜尋服務供應商識別碼。"),
                    ModelId = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false, comment: "核准模型的內部識別碼。"),
                    Operation = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, comment: "模型呼叫或背景工作的操作類型。"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "資料建立時間，採 UTC offset。"),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "工作開始執行時間。"),
                    FinishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "工作結束時間。"),
                    PriceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "此呼叫採用的不可變價格版本。"),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false, comment: "費用幣別代碼；不同幣別不可直接合計。"),
                    Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, comment: "成本類型。"),
                    InputPerMillion = table.Column<decimal>(type: "decimal(20,8)", precision: 20, scale: 8, nullable: false, comment: "每百萬輸入 tokens 的單價。"),
                    CachedInputPerMillion = table.Column<decimal>(type: "decimal(20,8)", precision: 20, scale: 8, nullable: false, comment: "每百萬快取輸入 tokens 的單價。"),
                    OutputPerMillion = table.Column<decimal>(type: "decimal(20,8)", precision: 20, scale: 8, nullable: false, comment: "每百萬輸出 tokens 的單價。"),
                    PerRequest = table.Column<decimal>(type: "decimal(20,8)", precision: 20, scale: 8, nullable: false, comment: "每次呼叫的固定單價。"),
                    RequestCharge = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, comment: "搜尋服務每次請求的費用。"),
                    InputTokens = table.Column<long>(type: "bigint", nullable: true, comment: "模型回報的輸入 tokens；未知保持空值。"),
                    CachedInputTokens = table.Column<long>(type: "bigint", nullable: true, comment: "模型回報的快取輸入 tokens。"),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: true, comment: "模型回報的輸出 tokens；未知保持空值。"),
                    ReasoningTokens = table.Column<long>(type: "bigint", nullable: true, comment: "模型回報的推理 tokens。"),
                    UsageComplete = table.Column<bool>(type: "bit", nullable: false, comment: "本次呼叫是否有完整且可計費的實際用量。"),
                    Amount = table.Column<decimal>(type: "decimal(20,8)", precision: 20, scale: 8, nullable: true, comment: "此呼叫的已知費用；未知保持空值。"),
                    State = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false, comment: "業務物件的生命週期狀態。"),
                    Outcome = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, comment: "搜尋或處理操作的結果分類。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelCharges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelCharges_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "conversations",
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ModelCharges_ModelPrices_PriceId",
                        column: x => x.PriceId,
                        principalSchema: "billing",
                        principalTable: "ModelPrices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ModelCharges_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "各呼叫當時的價格與用量快照；未知費用保持空值。");

            migrationBuilder.CreateTable(
                name: "ChunkEmbeddings1024",
                schema: "knowledge",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false, comment: "資料的主鍵識別碼。")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChunkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "向量對應的結構化片段 Guid 識別碼。"),
                    ProfileId = table.Column<int>(type: "int", nullable: false, comment: "向量空間及切段版本的 EmbeddingProfiles 外鍵。"),
                    ContentHash = table.Column<byte[]>(type: "binary(32)", nullable: false, comment: "實際向量輸入（文件名稱、標題路徑與本文）的 SHA-256。"),
                    Vector = table.Column<SqlVector<float>>(type: "vector(1024)", nullable: false, comment: "L2 正規化的 float32 向量；SQL Server 使用 VECTOR 型別。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChunkEmbeddings1024", x => x.Id)
                        .Annotation("SqlServer:Clustered", true);
                    table.ForeignKey(
                        name: "FK_ChunkEmbeddings1024_Chunks_ChunkId",
                        column: x => x.ChunkId,
                        principalSchema: "knowledge",
                        principalTable: "Chunks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChunkEmbeddings1024_EmbeddingProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalSchema: "knowledge",
                        principalTable: "EmbeddingProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "1024 維原生向量、片段關聯與 profile 內容快取。");

            migrationBuilder.CreateTable(
                name: "ChunkEmbeddings768",
                schema: "knowledge",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false, comment: "資料的主鍵識別碼。")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChunkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "向量對應的結構化片段 Guid 識別碼。"),
                    ProfileId = table.Column<int>(type: "int", nullable: false, comment: "向量空間及切段版本的 EmbeddingProfiles 外鍵。"),
                    ContentHash = table.Column<byte[]>(type: "binary(32)", nullable: false, comment: "實際向量輸入（文件名稱、標題路徑與本文）的 SHA-256。"),
                    Vector = table.Column<SqlVector<float>>(type: "vector(768)", nullable: false, comment: "L2 正規化的 float32 向量；SQL Server 使用 VECTOR 型別。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChunkEmbeddings768", x => x.Id)
                        .Annotation("SqlServer:Clustered", true);
                    table.ForeignKey(
                        name: "FK_ChunkEmbeddings768_Chunks_ChunkId",
                        column: x => x.ChunkId,
                        principalSchema: "knowledge",
                        principalTable: "Chunks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChunkEmbeddings768_EmbeddingProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalSchema: "knowledge",
                        principalTable: "EmbeddingProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "768 維原生向量、片段關聯與 profile 內容快取。");

            migrationBuilder.CreateTable(
                name: "Artifacts",
                schema: "artifacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料的主鍵識別碼。"),
                    Version = table.Column<int>(type: "int", nullable: false, comment: "業務版本號，用於歷史或樂觀並行控制。"),
                    SourceMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "此成果版本所引用的來源訊息識別碼。"),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "關聯專案的識別碼。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Artifacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Artifacts_Messages_SourceMessageId",
                        column: x => x.SourceMessageId,
                        principalSchema: "conversations",
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Artifacts_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "projects",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Artifacts_Resources_Id",
                        column: x => x.Id,
                        principalSchema: "collaboration",
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "使用者保存的成果文件與目前版本。");

            migrationBuilder.CreateTable(
                name: "GenerationRuns",
                schema: "inference",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料的主鍵識別碼。"),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。"),
                    TraceId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true, comment: "W3C 流程追蹤識別，僅由伺服器建立。"),
                    ParentSpanId = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true, comment: "排程來源的 W3C span 識別，重試沿用同一 trace。"),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "持久作業識別，跨佇列與重試保持不變。"),
                    ActiveOwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "仍在執行的擁有者；filtered unique index 限制每人一個生成。"),
                    ExecutorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "處理此次生成的伺服器程序識別碼。"),
                    LeaseExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "生成 executor 租約的到期時間。"),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "關聯對話的識別碼。"),
                    UserMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "此次生成的使用者提問訊息識別碼。"),
                    AssistantMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "此次生成的 AI 回答訊息識別碼。"),
                    ModelId = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false, comment: "核准模型的內部識別碼。"),
                    Provider = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, comment: "模型或搜尋服務供應商識別碼。"),
                    ProviderModelId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false, comment: "送往指定供應商的原生模型識別碼，與核准路由識別碼分開保存。"),
                    ParametersJson = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "執行參數的 JSON 快照，不含服務密鑰。"),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false, comment: "擁有者範圍內的冪等請求識別，避免重試重複處理。"),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, comment: "請求內容指紋，用於辨識冪等識別碼衝突。"),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, comment: "業務執行狀態。"),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "生成中或已完成的回答文字快照。"),
                    LastSequence = table.Column<long>(type: "bigint", nullable: false, comment: "最後已持久化的生成事件序號。"),
                    IssueCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true, comment: "伺服器產生的不透明問題查證代碼；每個問題個別識別。"),
                    ErrorCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true, comment: "對外安全的錯誤代碼，不含密碼或完整例外。"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "資料建立時間，採 UTC offset。"),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "工作開始執行時間。"),
                    FinishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "工作結束時間。"),
                    ReservedTokens = table.Column<long>(type: "bigint", nullable: false, comment: "生成預留的保守輸入加最大輸出 token；執行中或缺失 usage 時占用配額，未執行即取消釋放。"),
                    InputTokens = table.Column<long>(type: "bigint", nullable: true, comment: "模型回報的輸入 tokens；未知保持空值。"),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: true, comment: "模型回報的輸出 tokens；未知保持空值。"),
                    DurationMilliseconds = table.Column<long>(type: "bigint", nullable: true, comment: "從請求建立至終止的總耗時毫秒；包括排隊、生成、取消與失敗。"),
                    GenerationMilliseconds = table.Column<long>(type: "bigint", nullable: true, comment: "從生成開始至終止的耗時毫秒；未開始的請求保持空值。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GenerationRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GenerationRuns_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "conversations",
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GenerationRuns_Messages_AssistantMessageId",
                        column: x => x.AssistantMessageId,
                        principalSchema: "conversations",
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GenerationRuns_Messages_UserMessageId",
                        column: x => x.UserMessageId,
                        principalSchema: "conversations",
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GenerationRuns_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "聊天生成的持久狀態、冪等請求、執行租約、回答及用量。");

            migrationBuilder.CreateTable(
                name: "MessageAttachments",
                schema: "attachments",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "關聯訊息的識別碼。"),
                    AttachmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "引用的附件識別碼。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageAttachments", x => new { x.MessageId, x.AttachmentId });
                    table.ForeignKey(
                        name: "FK_MessageAttachments_Attachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalSchema: "attachments",
                        principalTable: "Attachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MessageAttachments_Messages_MessageId",
                        column: x => x.MessageId,
                        principalSchema: "conversations",
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "訊息與附件的關聯及呈現順序。");

            migrationBuilder.CreateTable(
                name: "MessageCitations",
                schema: "knowledge",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "關聯訊息的識別碼。"),
                    Number = table.Column<int>(type: "int", nullable: false, comment: "回答引用的順序編號。"),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "關聯知識文件的識別碼。"),
                    PageNumber = table.Column<int>(type: "int", nullable: false, comment: "文件頁碼，從 1 開始。"),
                    EndPage = table.Column<int>(type: "int", nullable: false, comment: "片段結束的原始文件頁碼。"),
                    Title = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false, comment: "介面顯示標題。"),
                    Excerpt = table.Column<string>(type: "nvarchar(800)", maxLength: 800, nullable: false, comment: "檢索或引用時保存的文字摘要。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageCitations", x => new { x.MessageId, x.Number });
                    table.ForeignKey(
                        name: "FK_MessageCitations_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "knowledge",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MessageCitations_Messages_MessageId",
                        column: x => x.MessageId,
                        principalSchema: "conversations",
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "回答生成當時的知識引用、文件頁碼與摘要快照。");

            migrationBuilder.CreateTable(
                name: "MessageFeedback",
                schema: "quality",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "關聯訊息的識別碼。"),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。"),
                    Rating = table.Column<int>(type: "int", nullable: false, comment: "使用者對回答的評分。"),
                    Reason = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false, comment: "回饋理由。"),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false, comment: "使用者提供的補充說明。"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "資料最後修改時間，採 UTC offset。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageFeedback", x => x.MessageId);
                    table.ForeignKey(
                        name: "FK_MessageFeedback_Messages_MessageId",
                        column: x => x.MessageId,
                        principalSchema: "conversations",
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MessageFeedback_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "使用者對 AI 回答的私人評分與意見。");

            migrationBuilder.CreateTable(
                name: "ArtifactRevisions",
                schema: "artifacts",
                columns: table => new
                {
                    ArtifactId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "關聯成果文件的識別碼。"),
                    Version = table.Column<int>(type: "int", nullable: false, comment: "業務版本號，用於歷史或樂觀並行控制。"),
                    AuthorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "建立此版本的使用者識別碼。"),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false, comment: "介面顯示標題。"),
                    Content = table.Column<string>(type: "nvarchar(max)", maxLength: 64000, nullable: false, comment: "此版本的成果內容。"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "資料建立時間，採 UTC offset。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArtifactRevisions", x => new { x.ArtifactId, x.Version });
                    table.ForeignKey(
                        name: "FK_ArtifactRevisions_Artifacts_ArtifactId",
                        column: x => x.ArtifactId,
                        principalSchema: "artifacts",
                        principalTable: "Artifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArtifactRevisions_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "成果文件不可變版本、內容與作者。");

            migrationBuilder.CreateTable(
                name: "SourceReferences",
                schema: "integrations",
                columns: table => new
                {
                    ArtifactId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "關聯成果文件的識別碼。"),
                    SourceId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, comment: "外部資料來源識別碼。"),
                    ExternalId = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false, comment: "外部來源中的紀錄識別碼。"),
                    Revision = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false, comment: "外部來源或 repository 的固定版本識別。"),
                    ImportedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "此來源版本明確匯入的時間。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceReferences", x => x.ArtifactId);
                    table.ForeignKey(
                        name: "FK_SourceReferences_Artifacts_ArtifactId",
                        column: x => x.ArtifactId,
                        principalSchema: "artifacts",
                        principalTable: "Artifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "外部來源匯入的識別、來源版本與匯入時間。");

            migrationBuilder.CreateTable(
                name: "RunEvents",
                schema: "inference",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "關聯 GenerationRuns 的識別碼。"),
                    Sequence = table.Column<long>(type: "bigint", nullable: false, comment: "事件在同一生成中的遞增序號。"),
                    Type = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, comment: "事件的種類。"),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, comment: "業務執行狀態。"),
                    Delta = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "生成文字增量或完整快照。"),
                    IssueCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true, comment: "伺服器產生的不透明問題查證代碼；每個問題個別識別。"),
                    ErrorCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true, comment: "對外安全的錯誤代碼，不含密碼或完整例外。"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "資料建立時間，採 UTC offset。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunEvents", x => new { x.RunId, x.Sequence });
                    table.ForeignKey(
                        name: "FK_RunEvents_GenerationRuns_RunId",
                        column: x => x.RunId,
                        principalSchema: "inference",
                        principalTable: "GenerationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "生成事件的有序 SSE 重播紀錄；完整生成狀態以 GenerationRuns 為準。");

            migrationBuilder.InsertData(
                schema: "accesscontrol",
                table: "Features",
                columns: new[] { "Id", "Enabled", "Name", "Route", "SortOrder" },
                values: new object[,]
                {
                    { "admin", true, "平台管理", "/admin", 90 },
                    { "artifacts", true, "成果文件", "/artifacts", 40 },
                    { "audit", true, "活動稽核", "/admin/audit", 92 },
                    { "chat", true, "對話", "/chat", 10 },
                    { "dashboard", true, "總覽", "/dashboard", 5 },
                    { "files", true, "檔案庫", "/files", 15 },
                    { "integrations", true, "資料來源", "/integrations", 80 },
                    { "knowledge", true, "知識庫", "/knowledge", 30 },
                    { "logs.detail", true, "日誌診斷詳情", "", 111 },
                    { "logs.export", true, "日誌匯出", "", 112 },
                    { "logs.query", true, "系統日誌", "/admin/logs", 110 },
                    { "monitoring", true, "即時監控", "/admin/monitoring", 91 },
                    { "projects", true, "專案", "/projects", 20 },
                    { "quality", true, "品質評測", "/quality", 60 },
                    { "repositories", true, "程式庫", "/repositories", 65 },
                    { "shared", true, "分享", "/shared", 50 },
                    { "tasks", true, "背景任務", "/tasks", 70 }
                });

            migrationBuilder.InsertData(
                schema: "accesscontrol",
                table: "RoleGroups",
                columns: new[] { "Id", "Enabled", "Name" },
                values: new object[,]
                {
                    { "administrators", true, "平台管理" },
                    { "workspace", true, "基本工作區" }
                });

            migrationBuilder.InsertData(
                schema: "accesscontrol",
                table: "Roles",
                columns: new[] { "Id", "Enabled", "Name" },
                values: new object[,]
                {
                    { "administrator", true, "平台管理員" },
                    { "member", true, "一般使用者" }
                });

            migrationBuilder.InsertData(
                schema: "accesscontrol",
                table: "RoleGroupFeatures",
                columns: new[] { "FeatureId", "GroupId" },
                values: new object[,]
                {
                    { "admin", "administrators" },
                    { "audit", "administrators" },
                    { "integrations", "administrators" },
                    { "logs.detail", "administrators" },
                    { "logs.export", "administrators" },
                    { "logs.query", "administrators" },
                    { "monitoring", "administrators" },
                    { "artifacts", "workspace" },
                    { "chat", "workspace" },
                    { "dashboard", "workspace" },
                    { "files", "workspace" },
                    { "knowledge", "workspace" },
                    { "projects", "workspace" },
                    { "quality", "workspace" },
                    { "repositories", "workspace" },
                    { "shared", "workspace" },
                    { "tasks", "workspace" }
                });

            migrationBuilder.InsertData(
                schema: "accesscontrol",
                table: "RoleGroupRoles",
                columns: new[] { "GroupId", "RoleId" },
                values: new object[,]
                {
                    { "administrators", "administrator" },
                    { "workspace", "member" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactRevisions_AuthorId",
                schema: "artifacts",
                table: "ArtifactRevisions",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Artifacts_ProjectId",
                schema: "artifacts",
                table: "Artifacts",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Artifacts_SourceMessageId",
                schema: "artifacts",
                table: "Artifacts",
                column: "SourceMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_OwnerId_CreatedAt",
                schema: "attachments",
                table: "Attachments",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_OwnerId_InLibrary_CreatedAt_Id",
                schema: "attachments",
                table: "Attachments",
                columns: new[] { "OwnerId", "InLibrary", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_StorageKey",
                schema: "attachments",
                table: "Attachments",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_StorageState_CreatedAt",
                schema: "attachments",
                table: "Attachments",
                columns: new[] { "StorageState", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_Action_Id",
                schema: "audit",
                table: "AuditEvents",
                columns: new[] { "Action", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_ActorId_Id",
                schema: "audit",
                table: "AuditEvents",
                columns: new[] { "ActorId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_At",
                schema: "audit",
                table: "AuditEvents",
                column: "At");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_ResourceId_Id",
                schema: "audit",
                table: "AuditEvents",
                columns: new[] { "ResourceId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobs_ActiveKey",
                schema: "jobs",
                table: "BackgroundJobs",
                column: "ActiveKey",
                unique: true,
                filter: "[ActiveKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobs_OwnerId_CreatedAt",
                schema: "jobs",
                table: "BackgroundJobs",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobs_ResourceId",
                schema: "jobs",
                table: "BackgroundJobs",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobs_Status_LeaseUntil_CreatedAt",
                schema: "jobs",
                table: "BackgroundJobs",
                columns: new[] { "Status", "LeaseUntil", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobs_SubjectId",
                schema: "jobs",
                table: "BackgroundJobs",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ChunkEmbeddings1024_ChunkId",
                schema: "knowledge",
                table: "ChunkEmbeddings1024",
                column: "ChunkId");

            migrationBuilder.CreateIndex(
                name: "IX_ChunkEmbeddings1024_ProfileId_ChunkId",
                schema: "knowledge",
                table: "ChunkEmbeddings1024",
                columns: new[] { "ProfileId", "ChunkId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChunkEmbeddings1024_ProfileId_ContentHash",
                schema: "knowledge",
                table: "ChunkEmbeddings1024",
                columns: new[] { "ProfileId", "ContentHash" });

            migrationBuilder.CreateIndex(
                name: "IX_ChunkEmbeddings768_ChunkId",
                schema: "knowledge",
                table: "ChunkEmbeddings768",
                column: "ChunkId");

            migrationBuilder.CreateIndex(
                name: "IX_ChunkEmbeddings768_ProfileId_ChunkId",
                schema: "knowledge",
                table: "ChunkEmbeddings768",
                columns: new[] { "ProfileId", "ChunkId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChunkEmbeddings768_ProfileId_ContentHash",
                schema: "knowledge",
                table: "ChunkEmbeddings768",
                columns: new[] { "ProfileId", "ContentHash" });

            migrationBuilder.CreateIndex(
                name: "IX_Chunks_DocumentId_Ordinal",
                schema: "knowledge",
                table: "Chunks",
                columns: new[] { "DocumentId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Chunks_SearchId",
                schema: "knowledge",
                table: "Chunks",
                column: "SearchId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversationCollections_CollectionId",
                schema: "knowledge",
                table: "ConversationCollections",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_OwnerId_IsDeleted_IsArchived_IsFavorite_UpdatedAt",
                schema: "conversations",
                table: "Conversations",
                columns: new[] { "OwnerId", "IsDeleted", "IsArchived", "IsFavorite", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_OwnerId_IsDeleted_UpdatedAt",
                schema: "conversations",
                table: "Conversations",
                columns: new[] { "OwnerId", "IsDeleted", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_ProjectId",
                schema: "conversations",
                table: "Conversations",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticEvents_At_LogId",
                schema: "diagnostics",
                table: "DiagnosticEvents",
                columns: new[] { "At", "LogId" },
                descending: new bool[0])
                .Annotation("SqlServer:Clustered", true);

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticEvents_Category_At_LogId",
                schema: "diagnostics",
                table: "DiagnosticEvents",
                columns: new[] { "Category", "At", "LogId" });

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticEvents_ErrorCode_At_LogId",
                schema: "diagnostics",
                table: "DiagnosticEvents",
                columns: new[] { "ErrorCode", "At", "LogId" },
                filter: "[ErrorCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticEvents_EventId_At_LogId",
                schema: "diagnostics",
                table: "DiagnosticEvents",
                columns: new[] { "EventId", "At", "LogId" });

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticEvents_EventName_At_LogId",
                schema: "diagnostics",
                table: "DiagnosticEvents",
                columns: new[] { "EventName", "At", "LogId" });

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticEvents_Instance_At_LogId",
                schema: "diagnostics",
                table: "DiagnosticEvents",
                columns: new[] { "Instance", "At", "LogId" },
                filter: "[Instance] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticEvents_IssueCode_At_LogId",
                schema: "diagnostics",
                table: "DiagnosticEvents",
                columns: new[] { "IssueCode", "At", "LogId" },
                filter: "[IssueCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticEvents_JobId_At_LogId",
                schema: "diagnostics",
                table: "DiagnosticEvents",
                columns: new[] { "JobId", "At", "LogId" },
                filter: "[JobId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticEvents_Level_At_LogId",
                schema: "diagnostics",
                table: "DiagnosticEvents",
                columns: new[] { "Level", "At", "LogId" });

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticEvents_OperationId_At_LogId",
                schema: "diagnostics",
                table: "DiagnosticEvents",
                columns: new[] { "OperationId", "At", "LogId" },
                filter: "[OperationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticEvents_RunId_At_LogId",
                schema: "diagnostics",
                table: "DiagnosticEvents",
                columns: new[] { "RunId", "At", "LogId" },
                filter: "[RunId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticEvents_TraceId_At_LogId",
                schema: "diagnostics",
                table: "DiagnosticEvents",
                columns: new[] { "TraceId", "At", "LogId" },
                filter: "[TraceId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_AttachmentId",
                schema: "knowledge",
                table: "Documents",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_CollectionId_Status_IsDeleted",
                schema: "knowledge",
                table: "Documents",
                columns: new[] { "CollectionId", "Status", "IsDeleted" })
                .Annotation("SqlServer:Include", new[] { "Id", "FileName", "ChunkCount" });

            migrationBuilder.CreateIndex(
                name: "IX_EmbeddingProfiles_Key",
                schema: "knowledge",
                table: "EmbeddingProfiles",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmbeddingProfiles_Status",
                schema: "knowledge",
                table: "EmbeddingProfiles",
                column: "Status",
                unique: true,
                filter: "[Status] = 'active'");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationResults_ReviewerId",
                schema: "quality",
                table: "EvaluationResults",
                column: "ReviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationRuns_JobId",
                schema: "quality",
                table: "EvaluationRuns",
                column: "JobId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationRuns_OwnerId",
                schema: "quality",
                table: "EvaluationRuns",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationRuns_SetId_CreatedAt",
                schema: "quality",
                table: "EvaluationRuns",
                columns: new[] { "SetId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GenerationRuns_ActiveOwnerId",
                schema: "inference",
                table: "GenerationRuns",
                column: "ActiveOwnerId",
                unique: true,
                filter: "[ActiveOwnerId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GenerationRuns_ActiveOwnerId_LeaseExpiresAt",
                schema: "inference",
                table: "GenerationRuns",
                columns: new[] { "ActiveOwnerId", "LeaseExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GenerationRuns_AssistantMessageId",
                schema: "inference",
                table: "GenerationRuns",
                column: "AssistantMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_GenerationRuns_ConversationId_CreatedAt",
                schema: "inference",
                table: "GenerationRuns",
                columns: new[] { "ConversationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GenerationRuns_OwnerId_CreatedAt",
                schema: "inference",
                table: "GenerationRuns",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GenerationRuns_OwnerId_IdempotencyKey",
                schema: "inference",
                table: "GenerationRuns",
                columns: new[] { "OwnerId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GenerationRuns_UserMessageId",
                schema: "inference",
                table: "GenerationRuns",
                column: "UserMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageAttachments_AttachmentId",
                schema: "attachments",
                table: "MessageAttachments",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageCitations_DocumentId",
                schema: "knowledge",
                table: "MessageCitations",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageFeedback_OwnerId_UpdatedAt",
                schema: "quality",
                table: "MessageFeedback",
                columns: new[] { "OwnerId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ConversationId_CreatedAt",
                schema: "conversations",
                table: "Messages",
                columns: new[] { "ConversationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ParentId",
                schema: "conversations",
                table: "Messages",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelCharges_ConversationId",
                schema: "billing",
                table: "ModelCharges",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelCharges_CreatedAt",
                schema: "billing",
                table: "ModelCharges",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ModelCharges_OwnerId_CreatedAt",
                schema: "billing",
                table: "ModelCharges",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelCharges_PriceId",
                schema: "billing",
                table: "ModelCharges",
                column: "PriceId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelInvocations_OwnerId_CreatedAt",
                schema: "inference",
                table: "ModelInvocations",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelPrices_CreatedBy",
                schema: "billing",
                table: "ModelPrices",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ModelPrices_Provider_ModelId_EffectiveAt",
                schema: "billing",
                table: "ModelPrices",
                columns: new[] { "Provider", "ModelId", "EffectiveAt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_OwnerId_DismissedAt_ReadAt_CreatedAt",
                schema: "notifications",
                table: "Notifications",
                columns: new[] { "OwnerId", "DismissedAt", "ReadAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_OwnerId_EventKey",
                schema: "notifications",
                table: "Notifications",
                columns: new[] { "OwnerId", "EventKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTemplates_ProjectId",
                schema: "projects",
                table: "ProjectTemplates",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_PromptTemplates_OwnerId_UpdatedAt",
                schema: "library",
                table: "PromptTemplates",
                columns: new[] { "OwnerId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryImports_DocumentId",
                schema: "repositories",
                table: "RepositoryImports",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryImports_OwnerId_CollectionId_Repository_Commit_Path",
                schema: "repositories",
                table: "RepositoryImports",
                columns: new[] { "OwnerId", "CollectionId", "Repository", "Commit", "Path" });

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryReviews_JobId",
                schema: "repositories",
                table: "RepositoryReviews",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryReviews_OwnerId_CreatedAt",
                schema: "repositories",
                table: "RepositoryReviews",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryReviews_OwnerId_IdempotencyKey",
                schema: "repositories",
                table: "RepositoryReviews",
                columns: new[] { "OwnerId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResourceAttachments_AttachmentId",
                schema: "attachments",
                table: "ResourceAttachments",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceGroups_GroupId",
                schema: "collaboration",
                table: "ResourceGroups",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceMembers_UserId",
                schema: "collaboration",
                table: "ResourceMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Resources_OwnerId_Kind_UpdatedAt",
                schema: "collaboration",
                table: "Resources",
                columns: new[] { "OwnerId", "Kind", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Resources_ParentId",
                schema: "collaboration",
                table: "Resources",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_RetrievalEvaluations_JobId",
                schema: "quality",
                table: "RetrievalEvaluations",
                column: "JobId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RetrievalEvaluations_OwnerId_CreatedAt",
                schema: "quality",
                table: "RetrievalEvaluations",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RoleGroupFeatures_FeatureId",
                schema: "accesscontrol",
                table: "RoleGroupFeatures",
                column: "FeatureId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleGroupRoles_GroupId",
                schema: "accesscontrol",
                table: "RoleGroupRoles",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ShareLinks_ExpiresAt",
                schema: "sharing",
                table: "ShareLinks",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_ShareLinks_OwnerId_CreatedAt",
                schema: "sharing",
                table: "ShareLinks",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ShareRecipients_UserId_ShareId",
                schema: "sharing",
                table: "ShareRecipients",
                columns: new[] { "UserId", "ShareId" });

            migrationBuilder.CreateIndex(
                name: "IX_SourceReferences_SourceId_ExternalId",
                schema: "integrations",
                table: "SourceReferences",
                columns: new[] { "SourceId", "ExternalId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                schema: "accesscontrol",
                table: "UserRoles",
                column: "RoleId");

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
                name: "IX_Users_Sid",
                schema: "identity",
                table: "Users",
                column: "Sid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebSearches_OwnerId_CreatedAt",
                schema: "websearch",
                table: "WebSearches",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WebSearches_OwnerId_IdempotencyKey",
                schema: "websearch",
                table: "WebSearches",
                columns: new[] { "OwnerId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebSearches_RunId",
                schema: "websearch",
                table: "WebSearches",
                column: "RunId");

            // Full-text search is optional: without the component or the 1028 word breaker, retrieval reports vector mode.
            // CREATE FULLTEXT CATALOG/INDEX cannot run inside a transaction.
            migrationBuilder.Sql("""
                IF CONVERT(int, SERVERPROPERTY('IsFullTextInstalled')) = 1
                    AND EXISTS (SELECT 1 FROM sys.fulltext_languages WHERE lcid = 1028)
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'KnowledgeSearch')
                        EXEC('CREATE FULLTEXT CATALOG [KnowledgeSearch]');
                    IF NOT EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('knowledge.Chunks'))
                        EXEC('CREATE FULLTEXT INDEX ON [knowledge].[Chunks] ([Text] LANGUAGE 1028, [HeadingPath] LANGUAGE 1028) KEY INDEX [IX_Chunks_SearchId] ON [KnowledgeSearch] WITH CHANGE_TRACKING AUTO');
                END
                ELSE RAISERROR(N'知識全文索引未建立：未安裝全文元件或繁體中文 1028 斷詞器；執行期會明確回報 vector 模式。', 10, 1) WITH NOWAIT;
                """, suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('knowledge.Chunks')) DROP FULLTEXT INDEX ON [knowledge].[Chunks];", suppressTransaction: true);
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'KnowledgeSearch') DROP FULLTEXT CATALOG [KnowledgeSearch];", suppressTransaction: true);

            migrationBuilder.DropTable(
                name: "AdministratorBootstraps",
                schema: "administration");

            migrationBuilder.DropTable(
                name: "ArtifactRevisions",
                schema: "artifacts");

            migrationBuilder.DropTable(
                name: "AuditEvents",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "ChunkEmbeddings1024",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "ChunkEmbeddings768",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "ConversationCollections",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "ConversationLabels",
                schema: "conversations");

            migrationBuilder.DropTable(
                name: "DiagnosticEvents",
                schema: "diagnostics");

            migrationBuilder.DropTable(
                name: "DocumentPages",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "EvaluationResults",
                schema: "quality");

            migrationBuilder.DropTable(
                name: "GroupModelPolicies",
                schema: "accesscontrol");

            migrationBuilder.DropTable(
                name: "MessageAttachments",
                schema: "attachments");

            migrationBuilder.DropTable(
                name: "MessageCitations",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "MessageFeedback",
                schema: "quality");

            migrationBuilder.DropTable(
                name: "ModelCharges",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "ModelInvocations",
                schema: "inference");

            migrationBuilder.DropTable(
                name: "ModelProfiles",
                schema: "inference");

            migrationBuilder.DropTable(
                name: "Notifications",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "ProjectTemplates",
                schema: "projects");

            migrationBuilder.DropTable(
                name: "PromptTemplates",
                schema: "library");

            migrationBuilder.DropTable(
                name: "RepositoryConnections",
                schema: "repositories");

            migrationBuilder.DropTable(
                name: "RepositoryImports",
                schema: "repositories");

            migrationBuilder.DropTable(
                name: "RepositoryReviewResults",
                schema: "repositories");

            migrationBuilder.DropTable(
                name: "ResourceAttachments",
                schema: "attachments");

            migrationBuilder.DropTable(
                name: "ResourceGroups",
                schema: "collaboration");

            migrationBuilder.DropTable(
                name: "ResourceMembers",
                schema: "collaboration");

            migrationBuilder.DropTable(
                name: "RetrievalEvaluationResults",
                schema: "quality");

            migrationBuilder.DropTable(
                name: "RoleGroupFeatures",
                schema: "accesscontrol");

            migrationBuilder.DropTable(
                name: "RoleGroupRoles",
                schema: "accesscontrol");

            migrationBuilder.DropTable(
                name: "RunEvents",
                schema: "inference");

            migrationBuilder.DropTable(
                name: "ShareRecipients",
                schema: "sharing");

            migrationBuilder.DropTable(
                name: "SourceReferences",
                schema: "integrations");

            migrationBuilder.DropTable(
                name: "UserModelPolicies",
                schema: "accesscontrol");

            migrationBuilder.DropTable(
                name: "UserPreferences",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "UserRoles",
                schema: "accesscontrol");

            migrationBuilder.DropTable(
                name: "WebSearches",
                schema: "websearch");

            migrationBuilder.DropTable(
                name: "Chunks",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "EmbeddingProfiles",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "EvaluationRuns",
                schema: "quality");

            migrationBuilder.DropTable(
                name: "ModelPrices",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "RepositoryReviews",
                schema: "repositories");

            migrationBuilder.DropTable(
                name: "RetrievalEvaluations",
                schema: "quality");

            migrationBuilder.DropTable(
                name: "Features",
                schema: "accesscontrol");

            migrationBuilder.DropTable(
                name: "RoleGroups",
                schema: "accesscontrol");

            migrationBuilder.DropTable(
                name: "GenerationRuns",
                schema: "inference");

            migrationBuilder.DropTable(
                name: "ShareLinks",
                schema: "sharing");

            migrationBuilder.DropTable(
                name: "Artifacts",
                schema: "artifacts");

            migrationBuilder.DropTable(
                name: "Roles",
                schema: "accesscontrol");

            migrationBuilder.DropTable(
                name: "Documents",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "EvaluationSets",
                schema: "quality");

            migrationBuilder.DropTable(
                name: "BackgroundJobs",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "Messages",
                schema: "conversations");

            migrationBuilder.DropTable(
                name: "Attachments",
                schema: "attachments");

            migrationBuilder.DropTable(
                name: "Collections",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "Conversations",
                schema: "conversations");

            migrationBuilder.DropTable(
                name: "Projects",
                schema: "projects");

            migrationBuilder.DropTable(
                name: "Resources",
                schema: "collaboration");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "identity");
        }
    }
}
