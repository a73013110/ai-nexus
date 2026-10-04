using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiNexus.BuildingBlocks.Migrations
{
    /// <inheritdoc />
    public partial class AdministrativeInspectionAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_Action_Id",
                schema: "operations",
                table: "AuditEvents",
                columns: new[] { "Action", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_ResourceId_Id",
                schema: "operations",
                table: "AuditEvents",
                columns: new[] { "ResourceId", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditEvents_Action_Id",
                schema: "operations",
                table: "AuditEvents");

            migrationBuilder.DropIndex(
                name: "IX_AuditEvents_ResourceId_Id",
                schema: "operations",
                table: "AuditEvents");
        }
    }
}
