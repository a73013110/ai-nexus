using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiNexus.Features.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IndependentActivityAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "access",
                table: "Features",
                columns: new[] { "Id", "Enabled", "Name", "Route", "SortOrder" },
                values: new object[] { "audit", true, "活動稽核", "/admin/audit", 92 });

            migrationBuilder.InsertData(
                schema: "access",
                table: "RoleGroupFeatures",
                columns: new[] { "FeatureId", "GroupId" },
                values: new object[] { "audit", "administrators" });

            // Preserve existing read access for custom groups that previously granted admin.
            migrationBuilder.Sql("""
                INSERT INTO [access].[RoleGroupFeatures] ([GroupId], [FeatureId])
                SELECT existing.[GroupId], N'audit'
                FROM [access].[RoleGroupFeatures] AS existing
                WHERE existing.[FeatureId] = N'admin'
                  AND NOT EXISTS (
                    SELECT 1 FROM [access].[RoleGroupFeatures] AS granted
                    WHERE granted.[GroupId] = existing.[GroupId] AND granted.[FeatureId] = N'audit');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM [access].[RoleGroupFeatures] WHERE [FeatureId] = N'audit';");

            migrationBuilder.DeleteData(
                schema: "access",
                table: "Features",
                keyColumn: "Id",
                keyValue: "audit");
        }
    }
}
