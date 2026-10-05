using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiNexus.BuildingBlocks.Migrations
{
    /// <inheritdoc />
    public partial class AccessControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "access");

            migrationBuilder.CreateTable(
                name: "Features",
                schema: "access",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Route = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Features", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoleGroups",
                schema: "access",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                schema: "access",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoleGroupFeatures",
                schema: "access",
                columns: table => new
                {
                    GroupId = table.Column<string>(type: "nvarchar(64)", nullable: false),
                    FeatureId = table.Column<string>(type: "nvarchar(64)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleGroupFeatures", x => new { x.GroupId, x.FeatureId });
                    table.ForeignKey(
                        name: "FK_RoleGroupFeatures_Features_FeatureId",
                        column: x => x.FeatureId,
                        principalSchema: "access",
                        principalTable: "Features",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoleGroupFeatures_RoleGroups_GroupId",
                        column: x => x.GroupId,
                        principalSchema: "access",
                        principalTable: "RoleGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoleGroupRoles",
                schema: "access",
                columns: table => new
                {
                    RoleId = table.Column<string>(type: "nvarchar(64)", nullable: false),
                    GroupId = table.Column<string>(type: "nvarchar(64)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleGroupRoles", x => new { x.RoleId, x.GroupId });
                    table.ForeignKey(
                        name: "FK_RoleGroupRoles_RoleGroups_GroupId",
                        column: x => x.GroupId,
                        principalSchema: "access",
                        principalTable: "RoleGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoleGroupRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "access",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                schema: "access",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(64)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "access",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "access",
                table: "Features",
                columns: new[] { "Id", "Enabled", "Name", "Route", "SortOrder" },
                values: new object[] { "chat", true, "AI 對話", "/chat", 10 });

            migrationBuilder.InsertData(
                schema: "access",
                table: "RoleGroups",
                columns: new[] { "Id", "Enabled", "Name" },
                values: new object[] { "workspace", true, "基本工作\u53f0" });

            migrationBuilder.InsertData(
                schema: "access",
                table: "Roles",
                columns: new[] { "Id", "Enabled", "Name" },
                values: new object[] { "member", true, "一般使用者" });

            migrationBuilder.InsertData(
                schema: "access",
                table: "RoleGroupFeatures",
                columns: new[] { "FeatureId", "GroupId" },
                values: new object[] { "chat", "workspace" });

            migrationBuilder.InsertData(
                schema: "access",
                table: "RoleGroupRoles",
                columns: new[] { "GroupId", "RoleId" },
                values: new object[] { "workspace", "member" });

            migrationBuilder.CreateIndex(
                name: "IX_RoleGroupFeatures_FeatureId",
                schema: "access",
                table: "RoleGroupFeatures",
                column: "FeatureId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleGroupRoles_GroupId",
                schema: "access",
                table: "RoleGroupRoles",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                schema: "access",
                table: "UserRoles",
                column: "RoleId");

            // Existing users receive the same initial role as new users. Later removals are respected.
            migrationBuilder.Sql("""
                INSERT INTO [access].[UserRoles] ([UserId], [RoleId])
                SELECT [Id], N'member' FROM [identity].[Users] AS u
                WHERE NOT EXISTS (SELECT 1 FROM [access].[UserRoles] AS r WHERE r.[UserId] = u.[Id] AND r.[RoleId] = N'member');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoleGroupFeatures",
                schema: "access");

            migrationBuilder.DropTable(
                name: "RoleGroupRoles",
                schema: "access");

            migrationBuilder.DropTable(
                name: "UserRoles",
                schema: "access");

            migrationBuilder.DropTable(
                name: "Features",
                schema: "access");

            migrationBuilder.DropTable(
                name: "RoleGroups",
                schema: "access");

            migrationBuilder.DropTable(
                name: "Roles",
                schema: "access");
        }
    }
}
