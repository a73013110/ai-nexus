using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiNexus.Features.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkspaceExperience : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TextContent",
                schema: "knowledge",
                table: "Documents",
                type: "nvarchar(max)",
                maxLength: 200000,
                nullable: true,
                comment: "純文字來源的可編輯內容；一般上傳原檔保持空值。");

            migrationBuilder.AddColumn<int>(
                name: "TextVersion",
                schema: "knowledge",
                table: "Documents",
                type: "int",
                nullable: false,
                defaultValue: 0,
                comment: "純文字內容的樂觀並行版本號。");

            migrationBuilder.CreateTable(
                name: "Notifications",
                schema: "operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料的主鍵識別碼。"),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。"),
                    Version = table.Column<int>(type: "int", nullable: false, comment: "業務版本號，用於歷史或樂觀並行控制。"),
                    EventKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false, comment: "通知來源事件的冪等識別碼。"),
                    Type = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false, comment: "事件的種類。"),
                    Severity = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, comment: "通知呈現層級：info、success 或 error。"),
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
                name: "RepositoryReviews",
                schema: "workspace",
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
                        principalSchema: "operations",
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
                name: "RepositoryReviewResults",
                schema: "workspace",
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
                        principalSchema: "workspace",
                        principalTable: "RepositoryReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "Review 各區段的持久結果及用量；重試沿用已完成區段。");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_OwnerId_DismissedAt_ReadAt_CreatedAt",
                schema: "operations",
                table: "Notifications",
                columns: new[] { "OwnerId", "DismissedAt", "ReadAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_OwnerId_EventKey",
                schema: "operations",
                table: "Notifications",
                columns: new[] { "OwnerId", "EventKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryReviews_JobId",
                schema: "workspace",
                table: "RepositoryReviews",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryReviews_OwnerId_CreatedAt",
                schema: "workspace",
                table: "RepositoryReviews",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryReviews_OwnerId_IdempotencyKey",
                schema: "workspace",
                table: "RepositoryReviews",
                columns: new[] { "OwnerId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notifications",
                schema: "operations");

            migrationBuilder.DropTable(
                name: "RepositoryReviewResults",
                schema: "workspace");

            migrationBuilder.DropTable(
                name: "RepositoryReviews",
                schema: "workspace");

            migrationBuilder.DropColumn(
                name: "TextContent",
                schema: "knowledge",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "TextVersion",
                schema: "knowledge",
                table: "Documents");
        }
    }
}
