using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AiNexus.Features.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NavigationGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "Features",
                schema: "accesscontrol",
                comment: "模組註冊的功能入口、顯示名稱、路由、選單分群及啟用狀態。",
                oldComment: "模組註冊的功能入口、顯示名稱、路由及啟用狀態。");

            migrationBuilder.AlterColumn<int>(
                name: "SortOrder",
                schema: "accesscontrol",
                table: "Features",
                type: "int",
                nullable: false,
                comment: "同一選單分群內的顯示順序；數值越小越前。",
                oldClrType: typeof(int),
                oldType: "int",
                oldComment: "介面顯示順序；數值越小越前。");

            migrationBuilder.AddColumn<string>(
                name: "GroupId",
                schema: "accesscontrol",
                table: "Features",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                comment: "所在的選單分群；null 表示位於選單最上層。");

            migrationBuilder.AddColumn<string>(
                name: "Icon",
                schema: "accesscontrol",
                table: "Features",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "",
                comment: "前端圖示名稱；未知名稱由前端改用預設圖示。");

            migrationBuilder.CreateTable(
                name: "NavigationGroups",
                schema: "accesscontrol",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, comment: "資料的主鍵識別碼。"),
                    ParentId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, comment: "上層分群；null 表示位於選單最上層。"),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false, comment: "業務物件的顯示名稱。"),
                    SortOrder = table.Column<int>(type: "int", nullable: false, comment: "同一層內的顯示順序；數值越小越前，同一層的功能排在分群之前。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NavigationGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NavigationGroups_NavigationGroups_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "accesscontrol",
                        principalTable: "NavigationGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "工作區選單的分群；可巢狀，只決定功能入口的排列，不影響授權。");

            migrationBuilder.UpdateData(
                schema: "accesscontrol",
                table: "Features",
                keyColumn: "Id",
                keyValue: "admin",
                columns: new[] { "GroupId", "Icon" },
                values: new object[] { "system", "lock" });

            migrationBuilder.UpdateData(
                schema: "accesscontrol",
                table: "Features",
                keyColumn: "Id",
                keyValue: "artifacts",
                columns: new[] { "GroupId", "Icon" },
                values: new object[] { "work", "document" });

            migrationBuilder.UpdateData(
                schema: "accesscontrol",
                table: "Features",
                keyColumn: "Id",
                keyValue: "audit",
                columns: new[] { "GroupId", "Icon" },
                values: new object[] { "system", "audit" });

            migrationBuilder.UpdateData(
                schema: "accesscontrol",
                table: "Features",
                keyColumn: "Id",
                keyValue: "chat",
                columns: new[] { "GroupId", "Icon" },
                values: new object[] { "work", "chat" });

            migrationBuilder.UpdateData(
                schema: "accesscontrol",
                table: "Features",
                keyColumn: "Id",
                keyValue: "dashboard",
                columns: new[] { "GroupId", "Icon" },
                values: new object[] { null, "dashboard" });

            migrationBuilder.UpdateData(
                schema: "accesscontrol",
                table: "Features",
                keyColumn: "Id",
                keyValue: "files",
                columns: new[] { "GroupId", "Icon" },
                values: new object[] { "work", "files" });

            migrationBuilder.UpdateData(
                schema: "accesscontrol",
                table: "Features",
                keyColumn: "Id",
                keyValue: "integrations",
                columns: new[] { "GroupId", "Icon" },
                values: new object[] { "system", "integrations" });

            migrationBuilder.UpdateData(
                schema: "accesscontrol",
                table: "Features",
                keyColumn: "Id",
                keyValue: "knowledge",
                columns: new[] { "GroupId", "Icon" },
                values: new object[] { "work", "library" });

            migrationBuilder.UpdateData(
                schema: "accesscontrol",
                table: "Features",
                keyColumn: "Id",
                keyValue: "logs.detail",
                columns: new[] { "GroupId", "Icon" },
                values: new object[] { "system", "search" });

            migrationBuilder.UpdateData(
                schema: "accesscontrol",
                table: "Features",
                keyColumn: "Id",
                keyValue: "logs.export",
                columns: new[] { "GroupId", "Icon" },
                values: new object[] { "system", "download" });

            migrationBuilder.UpdateData(
                schema: "accesscontrol",
                table: "Features",
                keyColumn: "Id",
                keyValue: "logs.query",
                columns: new[] { "GroupId", "Icon" },
                values: new object[] { "system", "logs" });

            migrationBuilder.UpdateData(
                schema: "accesscontrol",
                table: "Features",
                keyColumn: "Id",
                keyValue: "monitoring",
                columns: new[] { "GroupId", "Icon" },
                values: new object[] { "system", "activity" });

            migrationBuilder.UpdateData(
                schema: "accesscontrol",
                table: "Features",
                keyColumn: "Id",
                keyValue: "operations",
                columns: new[] { "GroupId", "Icon" },
                values: new object[] { "system", "chart" });

            migrationBuilder.UpdateData(
                schema: "accesscontrol",
                table: "Features",
                keyColumn: "Id",
                keyValue: "projects",
                columns: new[] { "GroupId", "Icon" },
                values: new object[] { "work", "projects" });

            migrationBuilder.UpdateData(
                schema: "accesscontrol",
                table: "Features",
                keyColumn: "Id",
                keyValue: "quality",
                columns: new[] { "GroupId", "Icon", "SortOrder" },
                values: new object[] { "system", "shield", 98 });

            migrationBuilder.UpdateData(
                schema: "accesscontrol",
                table: "Features",
                keyColumn: "Id",
                keyValue: "repositories",
                columns: new[] { "GroupId", "Icon" },
                values: new object[] { "collaboration", "git" });

            migrationBuilder.UpdateData(
                schema: "accesscontrol",
                table: "Features",
                keyColumn: "Id",
                keyValue: "shared",
                columns: new[] { "GroupId", "Icon" },
                values: new object[] { "collaboration", "share" });

            migrationBuilder.UpdateData(
                schema: "accesscontrol",
                table: "Features",
                keyColumn: "Id",
                keyValue: "tasks",
                columns: new[] { "GroupId", "Icon" },
                values: new object[] { "collaboration", "tasks" });

            migrationBuilder.InsertData(
                schema: "accesscontrol",
                table: "NavigationGroups",
                columns: new[] { "Id", "Name", "ParentId", "SortOrder" },
                values: new object[,]
                {
                    { "collaboration", "協作", null, 20 },
                    { "system", "系統管理", null, 30 },
                    { "work", "AI 工作台", null, 10 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Features_GroupId",
                schema: "accesscontrol",
                table: "Features",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_NavigationGroups_ParentId",
                schema: "accesscontrol",
                table: "NavigationGroups",
                column: "ParentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Features_NavigationGroups_GroupId",
                schema: "accesscontrol",
                table: "Features",
                column: "GroupId",
                principalSchema: "accesscontrol",
                principalTable: "NavigationGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Features_NavigationGroups_GroupId",
                schema: "accesscontrol",
                table: "Features");

            migrationBuilder.DropTable(
                name: "NavigationGroups",
                schema: "accesscontrol");

            migrationBuilder.DropIndex(
                name: "IX_Features_GroupId",
                schema: "accesscontrol",
                table: "Features");

            migrationBuilder.DropColumn(
                name: "GroupId",
                schema: "accesscontrol",
                table: "Features");

            migrationBuilder.DropColumn(
                name: "Icon",
                schema: "accesscontrol",
                table: "Features");

            migrationBuilder.AlterTable(
                name: "Features",
                schema: "accesscontrol",
                comment: "模組註冊的功能入口、顯示名稱、路由及啟用狀態。",
                oldComment: "模組註冊的功能入口、顯示名稱、路由、選單分群及啟用狀態。");

            migrationBuilder.AlterColumn<int>(
                name: "SortOrder",
                schema: "accesscontrol",
                table: "Features",
                type: "int",
                nullable: false,
                comment: "介面顯示順序；數值越小越前。",
                oldClrType: typeof(int),
                oldType: "int",
                oldComment: "同一選單分群內的顯示順序；數值越小越前。");

            migrationBuilder.UpdateData(
                schema: "accesscontrol",
                table: "Features",
                keyColumn: "Id",
                keyValue: "quality",
                column: "SortOrder",
                value: 65);
        }
    }
}
