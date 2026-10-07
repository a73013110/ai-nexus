using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AiNexus.BuildingBlocks.Migrations
{
    /// <inheritdoc />
    public partial class SystemDiagnostics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IssueCode",
                schema: "inference",
                table: "RunEvents",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true,
                comment: "伺服器產生的不透明問題查證代碼；每個問題個別識別。");

            migrationBuilder.AddColumn<string>(
                name: "IssueCode",
                schema: "operations",
                table: "Notifications",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true,
                comment: "伺服器產生的不透明問題查證代碼；每個問題個別識別。");

            migrationBuilder.AddColumn<string>(
                name: "IssueCode",
                schema: "conversations",
                table: "Messages",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true,
                comment: "伺服器產生的不透明問題查證代碼；每個問題個別識別。");

            migrationBuilder.AddColumn<string>(
                name: "IssueCode",
                schema: "inference",
                table: "GenerationRuns",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true,
                comment: "伺服器產生的不透明問題查證代碼；每個問題個別識別。");

            migrationBuilder.AddColumn<Guid>(
                name: "OperationId",
                schema: "inference",
                table: "GenerationRuns",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                comment: "持久作業識別，跨佇列與重試保持不變。");

            migrationBuilder.AddColumn<string>(
                name: "ParentSpanId",
                schema: "inference",
                table: "GenerationRuns",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true,
                comment: "排程來源的 W3C span 識別，重試沿用同一 trace。");

            migrationBuilder.AddColumn<string>(
                name: "TraceId",
                schema: "inference",
                table: "GenerationRuns",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true,
                comment: "W3C 流程追蹤識別，僅由伺服器建立。");

            migrationBuilder.AlterColumn<string>(
                name: "ErrorMessage",
                schema: "operations",
                table: "BackgroundJobs",
                type: "nvarchar(240)",
                maxLength: 240,
                nullable: true,
                comment: "固定安全提示與查證代碼；不可保存例外自由文字。",
                oldClrType: typeof(string),
                oldType: "nvarchar(240)",
                oldMaxLength: 240,
                oldNullable: true,
                oldComment: "經限制的錯誤說明。");

            migrationBuilder.AddColumn<string>(
                name: "IssueCode",
                schema: "operations",
                table: "BackgroundJobs",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true,
                comment: "伺服器產生的不透明問題查證代碼；每個問題個別識別。");

            migrationBuilder.AddColumn<Guid>(
                name: "OperationId",
                schema: "operations",
                table: "BackgroundJobs",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                comment: "持久作業識別，跨佇列與重試保持不變。");

            migrationBuilder.AddColumn<string>(
                name: "ParentSpanId",
                schema: "operations",
                table: "BackgroundJobs",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true,
                comment: "排程來源的 W3C span 識別，重試沿用同一 trace。");

            migrationBuilder.AddColumn<string>(
                name: "TraceId",
                schema: "operations",
                table: "BackgroundJobs",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true,
                comment: "W3C 流程追蹤識別，僅由伺服器建立。");

            migrationBuilder.AddColumn<string>(
                name: "IssueCode",
                schema: "operations",
                table: "AuditEvents",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true,
                comment: "伺服器產生的不透明問題查證代碼；每個問題個別識別。");

            migrationBuilder.AddColumn<Guid>(
                name: "OperationId",
                schema: "operations",
                table: "AuditEvents",
                type: "uniqueidentifier",
                nullable: true,
                comment: "持久作業識別，跨佇列與重試保持不變。");

            migrationBuilder.AddColumn<string>(
                name: "TraceId",
                schema: "operations",
                table: "AuditEvents",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true,
                comment: "W3C 流程追蹤識別，僅由伺服器建立。");

            migrationBuilder.CreateTable(
                name: "DiagnosticEvents",
                schema: "operations",
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

            migrationBuilder.InsertData(
                schema: "access",
                table: "Features",
                columns: new[] { "Id", "Enabled", "Name", "Route", "SortOrder" },
                values: new object[,]
                {
                    { "logs.detail", true, "日誌診斷詳情", "", 111 },
                    { "logs.export", true, "日誌匯出", "", 112 },
                    { "logs.query", true, "系統日誌", "/admin/logs", 110 }
                });

            migrationBuilder.InsertData(
                schema: "access",
                table: "RoleGroupFeatures",
                columns: new[] { "FeatureId", "GroupId" },
                values: new object[,]
                {
                    { "logs.detail", "administrators" },
                    { "logs.export", "administrators" },
                    { "logs.query", "administrators" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticEvents_At_LogId",
                schema: "operations",
                table: "DiagnosticEvents",
                columns: new[] { "At", "LogId" },
                descending: new bool[0])
                .Annotation("SqlServer:Clustered", true);

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticEvents_Category_At_LogId",
                schema: "operations",
                table: "DiagnosticEvents",
                columns: new[] { "Category", "At", "LogId" });

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticEvents_ErrorCode_At_LogId",
                schema: "operations",
                table: "DiagnosticEvents",
                columns: new[] { "ErrorCode", "At", "LogId" },
                filter: "[ErrorCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticEvents_EventId_At_LogId",
                schema: "operations",
                table: "DiagnosticEvents",
                columns: new[] { "EventId", "At", "LogId" });

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticEvents_EventName_At_LogId",
                schema: "operations",
                table: "DiagnosticEvents",
                columns: new[] { "EventName", "At", "LogId" });

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticEvents_Instance_At_LogId",
                schema: "operations",
                table: "DiagnosticEvents",
                columns: new[] { "Instance", "At", "LogId" },
                filter: "[Instance] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticEvents_IssueCode_At_LogId",
                schema: "operations",
                table: "DiagnosticEvents",
                columns: new[] { "IssueCode", "At", "LogId" },
                filter: "[IssueCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticEvents_JobId_At_LogId",
                schema: "operations",
                table: "DiagnosticEvents",
                columns: new[] { "JobId", "At", "LogId" },
                filter: "[JobId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticEvents_Level_At_LogId",
                schema: "operations",
                table: "DiagnosticEvents",
                columns: new[] { "Level", "At", "LogId" });

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticEvents_OperationId_At_LogId",
                schema: "operations",
                table: "DiagnosticEvents",
                columns: new[] { "OperationId", "At", "LogId" },
                filter: "[OperationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticEvents_RunId_At_LogId",
                schema: "operations",
                table: "DiagnosticEvents",
                columns: new[] { "RunId", "At", "LogId" },
                filter: "[RunId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticEvents_TraceId_At_LogId",
                schema: "operations",
                table: "DiagnosticEvents",
                columns: new[] { "TraceId", "At", "LogId" },
                filter: "[TraceId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiagnosticEvents",
                schema: "operations");

            migrationBuilder.DeleteData(
                schema: "access",
                table: "RoleGroupFeatures",
                keyColumns: new[] { "FeatureId", "GroupId" },
                keyValues: new object[] { "logs.detail", "administrators" });

            migrationBuilder.DeleteData(
                schema: "access",
                table: "RoleGroupFeatures",
                keyColumns: new[] { "FeatureId", "GroupId" },
                keyValues: new object[] { "logs.export", "administrators" });

            migrationBuilder.DeleteData(
                schema: "access",
                table: "RoleGroupFeatures",
                keyColumns: new[] { "FeatureId", "GroupId" },
                keyValues: new object[] { "logs.query", "administrators" });

            migrationBuilder.DeleteData(
                schema: "access",
                table: "Features",
                keyColumn: "Id",
                keyValue: "logs.detail");

            migrationBuilder.DeleteData(
                schema: "access",
                table: "Features",
                keyColumn: "Id",
                keyValue: "logs.export");

            migrationBuilder.DeleteData(
                schema: "access",
                table: "Features",
                keyColumn: "Id",
                keyValue: "logs.query");

            migrationBuilder.DropColumn(
                name: "IssueCode",
                schema: "inference",
                table: "RunEvents");

            migrationBuilder.DropColumn(
                name: "IssueCode",
                schema: "operations",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "IssueCode",
                schema: "conversations",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "IssueCode",
                schema: "inference",
                table: "GenerationRuns");

            migrationBuilder.DropColumn(
                name: "OperationId",
                schema: "inference",
                table: "GenerationRuns");

            migrationBuilder.DropColumn(
                name: "ParentSpanId",
                schema: "inference",
                table: "GenerationRuns");

            migrationBuilder.DropColumn(
                name: "TraceId",
                schema: "inference",
                table: "GenerationRuns");

            migrationBuilder.DropColumn(
                name: "IssueCode",
                schema: "operations",
                table: "BackgroundJobs");

            migrationBuilder.DropColumn(
                name: "OperationId",
                schema: "operations",
                table: "BackgroundJobs");

            migrationBuilder.DropColumn(
                name: "ParentSpanId",
                schema: "operations",
                table: "BackgroundJobs");

            migrationBuilder.DropColumn(
                name: "TraceId",
                schema: "operations",
                table: "BackgroundJobs");

            migrationBuilder.DropColumn(
                name: "IssueCode",
                schema: "operations",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "OperationId",
                schema: "operations",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "TraceId",
                schema: "operations",
                table: "AuditEvents");

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
                oldNullable: true,
                oldComment: "固定安全提示與查證代碼；不可保存例外自由文字。");
        }
    }
}
