using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiNexus.BuildingBlocks.Migrations
{
    /// <inheritdoc />
    public partial class RetrievalEvaluationReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                        principalSchema: "operations",
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
                name: "RetrievalEvaluationResults",
                schema: "quality",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "關聯生成或評測執行的識別碼。"),
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RetrievalEvaluationResults",
                schema: "quality");

            migrationBuilder.DropTable(
                name: "RetrievalEvaluations",
                schema: "quality");
        }
    }
}
