using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiNexus.BuildingBlocks.Migrations
{
    /// <inheritdoc />
    public partial class Integrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SourceReferences",
                schema: "content",
                columns: table => new
                {
                    ArtifactId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Revision = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    ImportedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
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
                });

            migrationBuilder.InsertData(
                schema: "access",
                table: "Features",
                columns: new[] { "Id", "Enabled", "Name", "Route", "SortOrder" },
                values: new object[] { "integrations", true, "系統整合", "/integrations", 80 });

            migrationBuilder.InsertData(
                schema: "access",
                table: "RoleGroupFeatures",
                columns: new[] { "FeatureId", "GroupId" },
                values: new object[] { "integrations", "administrators" });

            migrationBuilder.CreateIndex(
                name: "IX_SourceReferences_SourceId_ExternalId",
                schema: "content",
                table: "SourceReferences",
                columns: new[] { "SourceId", "ExternalId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SourceReferences",
                schema: "content");

            migrationBuilder.DeleteData(
                schema: "access",
                table: "RoleGroupFeatures",
                keyColumns: new[] { "FeatureId", "GroupId" },
                keyValues: new object[] { "integrations", "administrators" });

            migrationBuilder.DeleteData(
                schema: "access",
                table: "Features",
                keyColumn: "Id",
                keyValue: "integrations");
        }
    }
}
