using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiNexus.BuildingBlocks.Migrations
{
    /// <inheritdoc />
    public partial class Administration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DetailsJson",
                schema: "operations",
                table: "AuditEvents",
                type: "nvarchar(max)",
                maxLength: 12000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AdministratorBootstraps",
                schema: "access",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GrantedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "GroupModelPolicies",
                schema: "access",
                columns: table => new
                {
                    GroupId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AllowedModelsJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    DailyRequestLimit = table.Column<int>(type: "int", nullable: true),
                    StoredAttachmentLimitBytes = table.Column<long>(type: "bigint", nullable: true)
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
                });

            migrationBuilder.InsertData(
                schema: "access",
                table: "Features",
                columns: new[] { "Id", "Enabled", "Name", "Route", "SortOrder" },
                values: new object[] { "admin", true, "管理", "/admin", 90 });

            migrationBuilder.InsertData(
                schema: "access",
                table: "RoleGroups",
                columns: new[] { "Id", "Enabled", "Name" },
                values: new object[] { "administrators", true, "平台管理" });

            migrationBuilder.InsertData(
                schema: "access",
                table: "Roles",
                columns: new[] { "Id", "Enabled", "Name" },
                values: new object[] { "administrator", true, "平台管理員" });

            migrationBuilder.InsertData(
                schema: "access",
                table: "RoleGroupFeatures",
                columns: new[] { "FeatureId", "GroupId" },
                values: new object[] { "admin", "administrators" });

            migrationBuilder.InsertData(
                schema: "access",
                table: "RoleGroupRoles",
                columns: new[] { "GroupId", "RoleId" },
                values: new object[] { "administrators", "administrator" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdministratorBootstraps",
                schema: "access");

            migrationBuilder.DropTable(
                name: "GroupModelPolicies",
                schema: "access");

            migrationBuilder.DeleteData(
                schema: "access",
                table: "RoleGroupFeatures",
                keyColumns: new[] { "FeatureId", "GroupId" },
                keyValues: new object[] { "admin", "administrators" });

            migrationBuilder.DeleteData(
                schema: "access",
                table: "RoleGroupRoles",
                keyColumns: new[] { "GroupId", "RoleId" },
                keyValues: new object[] { "administrators", "administrator" });

            migrationBuilder.DeleteData(
                schema: "access",
                table: "Features",
                keyColumn: "Id",
                keyValue: "admin");

            migrationBuilder.DeleteData(
                schema: "access",
                table: "RoleGroups",
                keyColumn: "Id",
                keyValue: "administrators");

            migrationBuilder.DeleteData(
                schema: "access",
                table: "Roles",
                keyColumn: "Id",
                keyValue: "administrator");

            migrationBuilder.DropColumn(
                name: "DetailsJson",
                schema: "operations",
                table: "AuditEvents");
        }
    }
}
