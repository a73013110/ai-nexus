using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiNexus.Features.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SplitPlatformOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "accesscontrol",
                table: "Features",
                keyColumn: "Id",
                keyValue: "admin",
                column: "Name",
                value: "存取管理");

            migrationBuilder.UpdateData(
                schema: "accesscontrol",
                table: "Features",
                keyColumn: "Id",
                keyValue: "audit",
                column: "SortOrder",
                value: 97);

            migrationBuilder.UpdateData(
                schema: "accesscontrol",
                table: "Features",
                keyColumn: "Id",
                keyValue: "files",
                column: "SortOrder",
                value: 20);

            migrationBuilder.UpdateData(
                schema: "accesscontrol",
                table: "Features",
                keyColumn: "Id",
                keyValue: "integrations",
                column: "SortOrder",
                value: 92);

            migrationBuilder.UpdateData(
                schema: "accesscontrol",
                table: "Features",
                keyColumn: "Id",
                keyValue: "logs.detail",
                column: "SortOrder",
                value: 95);

            migrationBuilder.UpdateData(
                schema: "accesscontrol",
                table: "Features",
                keyColumn: "Id",
                keyValue: "logs.export",
                column: "SortOrder",
                value: 96);

            migrationBuilder.UpdateData(
                schema: "accesscontrol",
                table: "Features",
                keyColumn: "Id",
                keyValue: "logs.query",
                column: "SortOrder",
                value: 94);

            migrationBuilder.UpdateData(
                schema: "accesscontrol",
                table: "Features",
                keyColumn: "Id",
                keyValue: "monitoring",
                column: "SortOrder",
                value: 93);

            migrationBuilder.UpdateData(
                schema: "accesscontrol",
                table: "Features",
                keyColumn: "Id",
                keyValue: "projects",
                column: "SortOrder",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "accesscontrol",
                table: "Features",
                keyColumn: "Id",
                keyValue: "quality",
                column: "SortOrder",
                value: 65);

            migrationBuilder.UpdateData(
                schema: "accesscontrol",
                table: "Features",
                keyColumn: "Id",
                keyValue: "repositories",
                column: "SortOrder",
                value: 60);

            migrationBuilder.UpdateData(
                schema: "accesscontrol",
                table: "Features",
                keyColumn: "Id",
                keyValue: "tasks",
                column: "SortOrder",
                value: 55);

            migrationBuilder.InsertData(
                schema: "accesscontrol",
                table: "Features",
                columns: new[] { "Id", "Enabled", "Name", "Route", "SortOrder" },
                values: new object[] { "operations", true, "平台營運", "/admin/operations", 91 });

            migrationBuilder.InsertData(
                schema: "accesscontrol",
                table: "RoleGroupFeatures",
                columns: new[] { "FeatureId", "GroupId" },
                values: new object[] { "operations", "administrators" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "accesscontrol",
                table: "RoleGroupFeatures",
                keyColumns: new[] { "FeatureId", "GroupId" },
                keyValues: new object[] { "operations", "administrators" });

            migrationBuilder.DeleteData(
                schema: "accesscontrol",
                table: "Features",
                keyColumn: "Id",
                keyValue: "operations");

            migrationBuilder.UpdateData(
                schema: "accesscontrol",
                table: "Features",
                keyColumn: "Id",
                keyValue: "admin",
                column: "Name",
                value: "平台管理");

            migrationBuilder.UpdateData(
                schema: "accesscontrol",
                table: "Features",
                keyColumn: "Id",
                keyValue: "audit",
                column: "SortOrder",
                value: 92);

            migrationBuilder.UpdateData(
                schema: "accesscontrol",
                table: "Features",
                keyColumn: "Id",
                keyValue: "files",
                column: "SortOrder",
                value: 15);

            migrationBuilder.UpdateData(
                schema: "accesscontrol",
                table: "Features",
                keyColumn: "Id",
                keyValue: "integrations",
                column: "SortOrder",
                value: 80);

            migrationBuilder.UpdateData(
                schema: "accesscontrol",
                table: "Features",
                keyColumn: "Id",
                keyValue: "logs.detail",
                column: "SortOrder",
                value: 111);

            migrationBuilder.UpdateData(
                schema: "accesscontrol",
                table: "Features",
                keyColumn: "Id",
                keyValue: "logs.export",
                column: "SortOrder",
                value: 112);

            migrationBuilder.UpdateData(
                schema: "accesscontrol",
                table: "Features",
                keyColumn: "Id",
                keyValue: "logs.query",
                column: "SortOrder",
                value: 110);

            migrationBuilder.UpdateData(
                schema: "accesscontrol",
                table: "Features",
                keyColumn: "Id",
                keyValue: "monitoring",
                column: "SortOrder",
                value: 91);

            migrationBuilder.UpdateData(
                schema: "accesscontrol",
                table: "Features",
                keyColumn: "Id",
                keyValue: "projects",
                column: "SortOrder",
                value: 20);

            migrationBuilder.UpdateData(
                schema: "accesscontrol",
                table: "Features",
                keyColumn: "Id",
                keyValue: "quality",
                column: "SortOrder",
                value: 60);

            migrationBuilder.UpdateData(
                schema: "accesscontrol",
                table: "Features",
                keyColumn: "Id",
                keyValue: "repositories",
                column: "SortOrder",
                value: 65);

            migrationBuilder.UpdateData(
                schema: "accesscontrol",
                table: "Features",
                keyColumn: "Id",
                keyValue: "tasks",
                column: "SortOrder",
                value: 70);
        }
    }
}
