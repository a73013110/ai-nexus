using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiNexus.BuildingBlocks.Migrations
{
    /// <inheritdoc />
    public partial class Projects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "projects");

            migrationBuilder.AddColumn<Guid>(
                name: "ParentId",
                schema: "collaboration",
                table: "Resources",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                schema: "conversations",
                table: "Conversations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Projects",
                schema: "projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Instructions = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Projects_Resources_Id",
                        column: x => x.Id,
                        principalSchema: "collaboration",
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectTemplates",
                schema: "projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", maxLength: 12000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectTemplates_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "projects",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "access",
                table: "Features",
                columns: new[] { "Id", "Enabled", "Name", "Route", "SortOrder" },
                values: new object[] { "projects", true, "專案", "/projects", 20 });

            migrationBuilder.InsertData(
                schema: "access",
                table: "RoleGroupFeatures",
                columns: new[] { "FeatureId", "GroupId" },
                values: new object[] { "projects", "workspace" });

            migrationBuilder.CreateIndex(
                name: "IX_Resources_ParentId",
                schema: "collaboration",
                table: "Resources",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_ProjectId",
                schema: "conversations",
                table: "Conversations",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Artifacts_ProjectId",
                schema: "content",
                table: "Artifacts",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTemplates_ProjectId",
                schema: "projects",
                table: "ProjectTemplates",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_Artifacts_Projects_ProjectId",
                schema: "content",
                table: "Artifacts",
                column: "ProjectId",
                principalSchema: "projects",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_Projects_ProjectId",
                schema: "conversations",
                table: "Conversations",
                column: "ProjectId",
                principalSchema: "projects",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Resources_Resources_ParentId",
                schema: "collaboration",
                table: "Resources",
                column: "ParentId",
                principalSchema: "collaboration",
                principalTable: "Resources",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Artifacts_Projects_ProjectId",
                schema: "content",
                table: "Artifacts");

            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_Projects_ProjectId",
                schema: "conversations",
                table: "Conversations");

            migrationBuilder.DropForeignKey(
                name: "FK_Resources_Resources_ParentId",
                schema: "collaboration",
                table: "Resources");

            migrationBuilder.DropTable(
                name: "ProjectTemplates",
                schema: "projects");

            migrationBuilder.DropTable(
                name: "Projects",
                schema: "projects");

            migrationBuilder.DropIndex(
                name: "IX_Resources_ParentId",
                schema: "collaboration",
                table: "Resources");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_ProjectId",
                schema: "conversations",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Artifacts_ProjectId",
                schema: "content",
                table: "Artifacts");

            migrationBuilder.DeleteData(
                schema: "access",
                table: "RoleGroupFeatures",
                keyColumns: new[] { "FeatureId", "GroupId" },
                keyValues: new object[] { "projects", "workspace" });

            migrationBuilder.DeleteData(
                schema: "access",
                table: "Features",
                keyColumn: "Id",
                keyValue: "projects");

            migrationBuilder.DropColumn(
                name: "ParentId",
                schema: "collaboration",
                table: "Resources");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                schema: "conversations",
                table: "Conversations");
        }
    }
}
