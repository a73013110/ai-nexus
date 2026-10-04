using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiNexus.BuildingBlocks.Migrations
{
    /// <inheritdoc />
    public partial class Quality : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "quality");

            migrationBuilder.CreateTable(
                name: "EvaluationSets",
                schema: "quality",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CasesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationSets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvaluationSets_Resources_Id",
                        column: x => x.Id,
                        principalSchema: "collaboration",
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MessageFeedback",
                schema: "quality",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rating = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageFeedback", x => x.MessageId);
                    table.ForeignKey(
                        name: "FK_MessageFeedback_Messages_MessageId",
                        column: x => x.MessageId,
                        principalSchema: "conversations",
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MessageFeedback_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EvaluationRuns",
                schema: "quality",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SetVersion = table.Column<int>(type: "int", nullable: false),
                    SetTitle = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CasesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VariantsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvaluationRuns_BackgroundJobs_JobId",
                        column: x => x.JobId,
                        principalSchema: "operations",
                        principalTable: "BackgroundJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvaluationRuns_EvaluationSets_SetId",
                        column: x => x.SetId,
                        principalSchema: "quality",
                        principalTable: "EvaluationSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvaluationRuns_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EvaluationResults",
                schema: "quality",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseIndex = table.Column<int>(type: "int", nullable: false),
                    VariantIndex = table.Column<int>(type: "int", nullable: false),
                    Output = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Truncated = table.Column<bool>(type: "bit", nullable: false),
                    RequiredMatches = table.Column<int>(type: "int", nullable: false),
                    RequiredTotal = table.Column<int>(type: "int", nullable: false),
                    ForbiddenMatches = table.Column<int>(type: "int", nullable: false),
                    ElapsedMs = table.Column<long>(type: "bigint", nullable: false),
                    InputTokens = table.Column<long>(type: "bigint", nullable: true),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: true),
                    ReviewScore = table.Column<int>(type: "int", nullable: true),
                    ReviewNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ReviewerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationResults", x => new { x.RunId, x.CaseIndex, x.VariantIndex });
                    table.ForeignKey(
                        name: "FK_EvaluationResults_EvaluationRuns_RunId",
                        column: x => x.RunId,
                        principalSchema: "quality",
                        principalTable: "EvaluationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EvaluationResults_Users_ReviewerId",
                        column: x => x.ReviewerId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "access",
                table: "Features",
                columns: new[] { "Id", "Enabled", "Name", "Route", "SortOrder" },
                values: new object[] { "quality", true, "品質評測", "/quality", 60 });

            migrationBuilder.InsertData(
                schema: "access",
                table: "RoleGroupFeatures",
                columns: new[] { "FeatureId", "GroupId" },
                values: new object[] { "quality", "workspace" });

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationResults_ReviewerId",
                schema: "quality",
                table: "EvaluationResults",
                column: "ReviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationRuns_JobId",
                schema: "quality",
                table: "EvaluationRuns",
                column: "JobId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationRuns_OwnerId",
                schema: "quality",
                table: "EvaluationRuns",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationRuns_SetId_CreatedAt",
                schema: "quality",
                table: "EvaluationRuns",
                columns: new[] { "SetId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageFeedback_OwnerId_UpdatedAt",
                schema: "quality",
                table: "MessageFeedback",
                columns: new[] { "OwnerId", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EvaluationResults",
                schema: "quality");

            migrationBuilder.DropTable(
                name: "MessageFeedback",
                schema: "quality");

            migrationBuilder.DropTable(
                name: "EvaluationRuns",
                schema: "quality");

            migrationBuilder.DropTable(
                name: "EvaluationSets",
                schema: "quality");

            migrationBuilder.DeleteData(
                schema: "access",
                table: "RoleGroupFeatures",
                keyColumns: new[] { "FeatureId", "GroupId" },
                keyValues: new object[] { "quality", "workspace" });

            migrationBuilder.DeleteData(
                schema: "access",
                table: "Features",
                keyColumn: "Id",
                keyValue: "quality");
        }
    }
}
