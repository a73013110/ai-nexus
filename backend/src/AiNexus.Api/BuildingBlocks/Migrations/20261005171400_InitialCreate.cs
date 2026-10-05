using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AiNexus.BuildingBlocks.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "access");

            migrationBuilder.EnsureSchema(
                name: "content");

            migrationBuilder.EnsureSchema(
                name: "attachments");

            migrationBuilder.EnsureSchema(
                name: "operations");

            migrationBuilder.EnsureSchema(
                name: "knowledge");

            migrationBuilder.EnsureSchema(
                name: "conversations");

            migrationBuilder.EnsureSchema(
                name: "quality");

            migrationBuilder.EnsureSchema(
                name: "inference");

            migrationBuilder.EnsureSchema(
                name: "projects");

            migrationBuilder.EnsureSchema(
                name: "library");

            migrationBuilder.EnsureSchema(
                name: "workspace");

            migrationBuilder.EnsureSchema(
                name: "collaboration");

            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.CreateTable(
                name: "AuditEvents",
                schema: "operations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, comment: "資料的主鍵識別碼。")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。"),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "身分測試時實際發起操作的管理者；空值表示與 OwnerId 相同。"),
                    Action = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, comment: "稽核操作名稱。"),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "關聯或稽核對象的業務資源識別碼。"),
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
                name: "Features",
                schema: "access",
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
                    DisplayName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false, comment: "使用者或模型的介面顯示名稱。"),
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
                schema: "access",
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
                schema: "access",
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
                    DisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, comment: "使用者或模型的介面顯示名稱。"),
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
                schema: "access",
                columns: table => new
                {
                    GroupId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, comment: "關聯功能群組的識別碼。"),
                    AllowedModelsJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true, comment: "模型白名單 JSON；空值不增加限制，空陣列禁止生成。"),
                    DailyRequestLimit = table.Column<int>(type: "int", nullable: true, comment: "每日生成次數上限；群組限制取最低值，UTC 午夜重設。"),
                    StoredAttachmentLimitBytes = table.Column<long>(type: "bigint", nullable: true, comment: "個人附件儲存上限，以 bytes 計；群組限制取最低值。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupModelPolicies", x => x.GroupId);
                    table.ForeignKey(
                        name: "FK_GroupModelPolicies_RoleGroups_GroupId",
                        column: x => x.GroupId,
                        principalSchema: "access",
                        principalTable: "RoleGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "功能群組的模型白名單、每日生成及附件空間限制。");

            migrationBuilder.CreateTable(
                name: "RoleGroupFeatures",
                schema: "access",
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
                        principalSchema: "access",
                        principalTable: "Features",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoleGroupFeatures_RoleGroups_GroupId",
                        column: x => x.GroupId,
                        principalSchema: "access",
                        principalTable: "RoleGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "功能群組與功能的授權關聯；有效功能取聯集。");

            migrationBuilder.CreateTable(
                name: "RoleGroupRoles",
                schema: "access",
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
                        principalSchema: "access",
                        principalTable: "RoleGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoleGroupRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "access",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "角色與功能群組的授權關聯。");

            migrationBuilder.CreateTable(
                name: "AdministratorBootstraps",
                schema: "access",
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
                    Kind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, comment: "業務操作、資源或成本的種類。"),
                    ModelId = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false, comment: "核准模型的內部識別碼。"),
                    Provider = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, comment: "模型或搜尋服務供應商識別碼。"),
                    DurationMilliseconds = table.Column<long>(type: "bigint", nullable: true, comment: "從請求建立至終止的總耗時毫秒；包括排隊、生成、取消與失敗。"),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, comment: "業務執行狀態。"),
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
                schema: "inference",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料的主鍵識別碼。"),
                    Provider = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, comment: "模型或搜尋服務供應商識別碼。"),
                    ModelId = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false, comment: "核准模型的內部識別碼。"),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false, comment: "費用幣別代碼；不同幣別不可直接合計。"),
                    Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, comment: "業務操作、資源或成本的種類。"),
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
                name: "PromptTemplates",
                schema: "library",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料的主鍵識別碼。"),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。"),
                    Title = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false, comment: "介面顯示標題。"),
                    Content = table.Column<string>(type: "nvarchar(max)", maxLength: 12000, nullable: false, comment: "訊息、版本或生成的文字內容。"),
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
                schema: "workspace",
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
                    Kind = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false, comment: "業務操作、資源或成本的種類。"),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false, comment: "業務物件的顯示名稱。"),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "父訊息或父資源識別碼，用於分支或階層繼承。"),
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
                name: "UserPreferences",
                schema: "identity",
                columns: table => new
                {
                    NexusUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "個人偏好對應使用者的主鍵與外鍵。"),
                    Theme = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false, comment: "外觀偏好：system、light 或 dark。"),
                    ReducedMotion = table.Column<bool>(type: "bit", nullable: false, comment: "是否減少動畫與動態效果。"),
                    DefaultModelId = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true, comment: "偏好的核准模型識別碼；空值使用伺服器預設。"),
                    ReadingFontSize = table.Column<int>(type: "int", nullable: false, defaultValue: 17, comment: "對話文字大小，以 CSS px 的偏好值記錄。"),
                    ReadingLineHeight = table.Column<double>(type: "float", nullable: false, defaultValue: 1.8, comment: "對話閱讀行高倍率。"),
                    Density = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, defaultValue: "comfortable", comment: "介面密度偏好。"),
                    SidebarWidth = table.Column<int>(type: "int", nullable: false, defaultValue: 264, comment: "側欄寬度偏好。"),
                    ReadingWidth = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, defaultValue: "standard", comment: "閱讀區寬度偏好。"),
                    EnterToSend = table.Column<bool>(type: "bit", nullable: false, defaultValue: true, comment: "是否以 Enter 送出提問；IME 組字不送出。"),
                    AutoFollow = table.Column<bool>(type: "bit", nullable: false, defaultValue: true, comment: "生成時是否跟隨最新回答。"),
                    SaveLocalDrafts = table.Column<bool>(type: "bit", nullable: false, defaultValue: true, comment: "是否在瀏覽器按使用者保存草稿。"),
                    NotifyOnCompletion = table.Column<bool>(type: "bit", nullable: false, comment: "是否在背景分頁提醒回答完成。"),
                    DefaultReasoningEffort = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, defaultValue: "auto", comment: "偏好的推理強度。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPreferences", x => x.NexusUserId);
                    table.ForeignKey(
                        name: "FK_UserPreferences_Users_NexusUserId",
                        column: x => x.NexusUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "使用者個人外觀、閱讀、對話操作與通知偏好；不含服務密鑰。");

            migrationBuilder.CreateTable(
                name: "UserRoles",
                schema: "access",
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
                        principalSchema: "access",
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
                schema: "inference",
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
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "關聯生成或評測執行的識別碼。")
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
                schema: "operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料的主鍵識別碼。"),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。"),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "關聯或稽核對象的業務資源識別碼。"),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "操作所關聯的業務對象識別碼。"),
                    Kind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, comment: "業務操作、資源或成本的種類。"),
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
                    ErrorCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true, comment: "對外安全的錯誤代碼，不含密碼或完整例外。"),
                    ErrorMessage = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: true, comment: "經限制的錯誤說明。"),
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
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "關聯或稽核對象的業務資源識別碼。"),
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
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "關聯或稽核對象的業務資源識別碼。"),
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
                        principalSchema: "access",
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
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "關聯或稽核對象的業務資源識別碼。"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "關聯使用者的 Users 主鍵。"),
                    Role = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false, comment: "訊息角色（user／assistant）或資源成員的閱讀／編輯權限。")
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
                schema: "collaboration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料的主鍵識別碼。"),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。"),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "外部資料來源識別碼。"),
                    Kind = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false, comment: "業務操作、資源或成本的種類。"),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false, comment: "介面顯示標題。"),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "分享時的固定內容快照；不隨後續編輯變動。"),
                    IncludeAttachments = table.Column<bool>(type: "bit", nullable: false, comment: "是否明確允許分享附件。"),
                    IsRevoked = table.Column<bool>(type: "bit", nullable: false, comment: "分享是否已撤銷。"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "資料建立時間，採 UTC offset。"),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "分享或授權到期時間。")
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
                    EmbeddingProfile = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true, comment: "向量模型、維度與前處理的版本指紋；不混用不同 profile。"),
                    Warning = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true, comment: "處理過程中的非致命提示。"),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, comment: "是否邏輯刪除；不自動刪除歷史紀錄。"),
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "關聯背景工作識別碼。")
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
                        principalSchema: "operations",
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
                    Content = table.Column<string>(type: "nvarchar(max)", maxLength: 12000, nullable: false, comment: "訊息、版本或生成的文字內容。")
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
                schema: "collaboration",
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
                        principalSchema: "collaboration",
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
                name: "Chunks",
                schema: "knowledge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料的主鍵識別碼。"),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "關聯知識文件的識別碼。"),
                    PageNumber = table.Column<int>(type: "int", nullable: false, comment: "文件頁碼，從 1 開始。"),
                    Ordinal = table.Column<int>(type: "int", nullable: false, comment: "同一父物件內的呈現順序。"),
                    Text = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false, comment: "文件頁面／片段的擷取文字。"),
                    EmbeddingJson = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "正規化向量的 JSON 表示；與 embedding profile 一起判斷相容性。"),
                    EmbeddingProfile = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true, comment: "向量模型、維度與前處理的版本指紋；不混用不同 profile。")
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
                comment: "知識檢索片段、頁碼、摘要與向量；查詢先套用資料 ACL。");

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
                schema: "knowledge",
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
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "關聯生成或評測執行的識別碼。"),
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
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "父訊息或父資源識別碼，用於分支或階層繼承。"),
                    Role = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, comment: "訊息角色（user／assistant）或資源成員的閱讀／編輯權限。"),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "訊息、版本或生成的文字內容。"),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, comment: "業務執行狀態。"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "資料建立時間，採 UTC offset。"),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true, comment: "關聯生成或評測執行的識別碼。"),
                    ModelId = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true, comment: "核准模型的內部識別碼。"),
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
                schema: "inference",
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
                    Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, comment: "業務操作、資源或成本的種類。"),
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
                        principalSchema: "inference",
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
                name: "Artifacts",
                schema: "content",
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
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false, comment: "訊息、版本或生成的文字內容。"),
                    LastSequence = table.Column<long>(type: "bigint", nullable: false, comment: "最後已持久化的生成事件序號。"),
                    ErrorCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true, comment: "對外安全的錯誤代碼，不含密碼或完整例外。"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "資料建立時間，採 UTC offset。"),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "工作開始執行時間。"),
                    FinishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "工作結束時間。"),
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
                    Reason = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false, comment: "回饋或操作理由。"),
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
                schema: "content",
                columns: table => new
                {
                    ArtifactId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "關聯成果文件的識別碼。"),
                    Version = table.Column<int>(type: "int", nullable: false, comment: "業務版本號，用於歷史或樂觀並行控制。"),
                    AuthorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "建立此版本的使用者識別碼。"),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false, comment: "介面顯示標題。"),
                    Content = table.Column<string>(type: "nvarchar(max)", maxLength: 64000, nullable: false, comment: "訊息、版本或生成的文字內容。"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "資料建立時間，採 UTC offset。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArtifactRevisions", x => new { x.ArtifactId, x.Version });
                    table.ForeignKey(
                        name: "FK_ArtifactRevisions_Artifacts_ArtifactId",
                        column: x => x.ArtifactId,
                        principalSchema: "content",
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
                schema: "content",
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
                        principalSchema: "content",
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
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "關聯生成或評測執行的識別碼。"),
                    Sequence = table.Column<long>(type: "bigint", nullable: false, comment: "事件在同一生成中的遞增序號。"),
                    Type = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, comment: "事件的種類。"),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, comment: "業務執行狀態。"),
                    Delta = table.Column<string>(type: "nvarchar(max)", nullable: true, comment: "生成文字增量或完整快照。"),
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
                schema: "access",
                table: "Features",
                columns: new[] { "Id", "Enabled", "Name", "Route", "SortOrder" },
                values: new object[,]
                {
                    { "admin", true, "管理", "/admin", 90 },
                    { "artifacts", true, "成果文件", "/artifacts", 40 },
                    { "chat", true, "AI 對話", "/chat", 10 },
                    { "dashboard", true, "總覽", "/dashboard", 5 },
                    { "integrations", true, "系統整合", "/integrations", 80 },
                    { "knowledge", true, "知識庫", "/knowledge", 30 },
                    { "projects", true, "專案", "/projects", 20 },
                    { "quality", true, "品質評測", "/quality", 60 },
                    { "repositories", true, "程式庫", "/repositories", 65 },
                    { "shared", true, "分享", "/shared", 50 },
                    { "tasks", true, "背景任務", "/tasks", 70 }
                });

            migrationBuilder.InsertData(
                schema: "access",
                table: "RoleGroups",
                columns: new[] { "Id", "Enabled", "Name" },
                values: new object[,]
                {
                    { "administrators", true, "平台管理" },
                    { "workspace", true, "基本工作區" }
                });

            migrationBuilder.InsertData(
                schema: "access",
                table: "Roles",
                columns: new[] { "Id", "Enabled", "Name" },
                values: new object[,]
                {
                    { "administrator", true, "平台管理員" },
                    { "member", true, "一般使用者" }
                });

            migrationBuilder.InsertData(
                schema: "access",
                table: "RoleGroupFeatures",
                columns: new[] { "FeatureId", "GroupId" },
                values: new object[,]
                {
                    { "admin", "administrators" },
                    { "integrations", "administrators" },
                    { "artifacts", "workspace" },
                    { "chat", "workspace" },
                    { "dashboard", "workspace" },
                    { "knowledge", "workspace" },
                    { "projects", "workspace" },
                    { "quality", "workspace" },
                    { "repositories", "workspace" },
                    { "shared", "workspace" },
                    { "tasks", "workspace" }
                });

            migrationBuilder.InsertData(
                schema: "access",
                table: "RoleGroupRoles",
                columns: new[] { "GroupId", "RoleId" },
                values: new object[,]
                {
                    { "administrators", "administrator" },
                    { "workspace", "member" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactRevisions_AuthorId",
                schema: "content",
                table: "ArtifactRevisions",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Artifacts_ProjectId",
                schema: "content",
                table: "Artifacts",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Artifacts_SourceMessageId",
                schema: "content",
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
                schema: "operations",
                table: "AuditEvents",
                columns: new[] { "Action", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_ActorId_Id",
                schema: "operations",
                table: "AuditEvents",
                columns: new[] { "ActorId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_At",
                schema: "operations",
                table: "AuditEvents",
                column: "At");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_ResourceId_Id",
                schema: "operations",
                table: "AuditEvents",
                columns: new[] { "ResourceId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobs_ActiveKey",
                schema: "operations",
                table: "BackgroundJobs",
                column: "ActiveKey",
                unique: true,
                filter: "[ActiveKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobs_OwnerId_CreatedAt",
                schema: "operations",
                table: "BackgroundJobs",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobs_ResourceId",
                schema: "operations",
                table: "BackgroundJobs",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobs_Status_LeaseUntil_CreatedAt",
                schema: "operations",
                table: "BackgroundJobs",
                columns: new[] { "Status", "LeaseUntil", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Chunks_DocumentId_Ordinal",
                schema: "knowledge",
                table: "Chunks",
                columns: new[] { "DocumentId", "Ordinal" },
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
                name: "IX_Documents_AttachmentId",
                schema: "knowledge",
                table: "Documents",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_CollectionId_Status",
                schema: "knowledge",
                table: "Documents",
                columns: new[] { "CollectionId", "Status" });

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
                schema: "inference",
                table: "ModelCharges",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelCharges_CreatedAt",
                schema: "inference",
                table: "ModelCharges",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ModelCharges_OwnerId_CreatedAt",
                schema: "inference",
                table: "ModelCharges",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelCharges_PriceId",
                schema: "inference",
                table: "ModelCharges",
                column: "PriceId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelInvocations_OwnerId_CreatedAt",
                schema: "inference",
                table: "ModelInvocations",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelPrices_CreatedBy",
                schema: "inference",
                table: "ModelPrices",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ModelPrices_Provider_ModelId_EffectiveAt",
                schema: "inference",
                table: "ModelPrices",
                columns: new[] { "Provider", "ModelId", "EffectiveAt" },
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
                schema: "knowledge",
                table: "RepositoryImports",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryImports_OwnerId_CollectionId_Repository_Commit_Path",
                schema: "knowledge",
                table: "RepositoryImports",
                columns: new[] { "OwnerId", "CollectionId", "Repository", "Commit", "Path" });

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
                name: "IX_RoleGroupFeatures_FeatureId",
                schema: "access",
                table: "RoleGroupFeatures",
                column: "FeatureId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleGroupRoles_GroupId",
                schema: "access",
                table: "RoleGroupRoles",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ShareLinks_ExpiresAt",
                schema: "collaboration",
                table: "ShareLinks",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_ShareLinks_OwnerId_CreatedAt",
                schema: "collaboration",
                table: "ShareLinks",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ShareRecipients_UserId_ShareId",
                schema: "collaboration",
                table: "ShareRecipients",
                columns: new[] { "UserId", "ShareId" });

            migrationBuilder.CreateIndex(
                name: "IX_SourceReferences_SourceId_ExternalId",
                schema: "content",
                table: "SourceReferences",
                columns: new[] { "SourceId", "ExternalId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                schema: "access",
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
                schema: "inference",
                table: "WebSearches",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WebSearches_OwnerId_IdempotencyKey",
                schema: "inference",
                table: "WebSearches",
                columns: new[] { "OwnerId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebSearches_RunId",
                schema: "inference",
                table: "WebSearches",
                column: "RunId");

            if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
            {
                migrationBuilder.Sql("IF CONVERT(int,SERVERPROPERTY('ProductMajorVersion')) >= 17 EXEC(N'ALTER TABLE [knowledge].[Chunks] ADD [EmbeddingVector] VECTOR(768) NULL, [EmbeddingVector1024] VECTOR(1024) NULL');");
                // Freeze this baseline's descriptions so direct DBA execution matches initialization.
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
                     (N'attachments', N'站外原檔的 metadata、儲存識別、權限與引用關聯；不含原檔 bytes。'),
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdministratorBootstraps",
                schema: "access");

            migrationBuilder.DropTable(
                name: "ArtifactRevisions",
                schema: "content");

            migrationBuilder.DropTable(
                name: "AuditEvents",
                schema: "operations");

            migrationBuilder.DropTable(
                name: "Chunks",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "ConversationCollections",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "ConversationLabels",
                schema: "conversations");

            migrationBuilder.DropTable(
                name: "DocumentPages",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "EvaluationResults",
                schema: "quality");

            migrationBuilder.DropTable(
                name: "GroupModelPolicies",
                schema: "access");

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
                schema: "inference");

            migrationBuilder.DropTable(
                name: "ModelInvocations",
                schema: "inference");

            migrationBuilder.DropTable(
                name: "ModelProfiles",
                schema: "inference");

            migrationBuilder.DropTable(
                name: "ProjectTemplates",
                schema: "projects");

            migrationBuilder.DropTable(
                name: "PromptTemplates",
                schema: "library");

            migrationBuilder.DropTable(
                name: "RepositoryConnections",
                schema: "workspace");

            migrationBuilder.DropTable(
                name: "RepositoryImports",
                schema: "knowledge");

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
                name: "RoleGroupFeatures",
                schema: "access");

            migrationBuilder.DropTable(
                name: "RoleGroupRoles",
                schema: "access");

            migrationBuilder.DropTable(
                name: "RunEvents",
                schema: "inference");

            migrationBuilder.DropTable(
                name: "ShareRecipients",
                schema: "collaboration");

            migrationBuilder.DropTable(
                name: "SourceReferences",
                schema: "content");

            migrationBuilder.DropTable(
                name: "UserPreferences",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "UserRoles",
                schema: "access");

            migrationBuilder.DropTable(
                name: "WebSearches",
                schema: "inference");

            migrationBuilder.DropTable(
                name: "EvaluationRuns",
                schema: "quality");

            migrationBuilder.DropTable(
                name: "ModelPrices",
                schema: "inference");

            migrationBuilder.DropTable(
                name: "Documents",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "Features",
                schema: "access");

            migrationBuilder.DropTable(
                name: "RoleGroups",
                schema: "access");

            migrationBuilder.DropTable(
                name: "GenerationRuns",
                schema: "inference");

            migrationBuilder.DropTable(
                name: "ShareLinks",
                schema: "collaboration");

            migrationBuilder.DropTable(
                name: "Artifacts",
                schema: "content");

            migrationBuilder.DropTable(
                name: "Roles",
                schema: "access");

            migrationBuilder.DropTable(
                name: "BackgroundJobs",
                schema: "operations");

            migrationBuilder.DropTable(
                name: "EvaluationSets",
                schema: "quality");

            migrationBuilder.DropTable(
                name: "Attachments",
                schema: "attachments");

            migrationBuilder.DropTable(
                name: "Collections",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "Messages",
                schema: "conversations");

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
