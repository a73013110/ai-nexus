using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiNexus.Features.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DesignWorkbenchFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "accesscontrol",
                table: "Features",
                columns: new[] { "Id", "Enabled", "GroupId", "Icon", "Name", "Route", "SortOrder" },
                values: new object[] { "design", true, "system", "sliders", "介面元件", "/design", 99 });

            migrationBuilder.InsertData(
                schema: "accesscontrol",
                table: "RoleGroupFeatures",
                columns: new[] { "FeatureId", "GroupId" },
                values: new object[] { "design", "administrators" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "accesscontrol",
                table: "RoleGroupFeatures",
                keyColumns: new[] { "FeatureId", "GroupId" },
                keyValues: new object[] { "design", "administrators" });

            migrationBuilder.DeleteData(
                schema: "accesscontrol",
                table: "Features",
                keyColumn: "Id",
                keyValue: "design");
        }
    }
}
