using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiNexus.Features.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UsageDateIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ModelInvocations_CreatedAt",
                schema: "inference",
                table: "ModelInvocations",
                column: "CreatedAt")
                .Annotation("SqlServer:Include", new[] { "OwnerId", "Kind", "ModelId", "Status", "InputTokens", "OutputTokens", "DurationMilliseconds" });

            migrationBuilder.CreateIndex(
                name: "IX_GenerationRuns_CreatedAt",
                schema: "inference",
                table: "GenerationRuns",
                column: "CreatedAt")
                .Annotation("SqlServer:Include", new[] { "OwnerId", "ModelId", "Status", "InputTokens", "OutputTokens", "DurationMilliseconds" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModelInvocations_CreatedAt",
                schema: "inference",
                table: "ModelInvocations");

            migrationBuilder.DropIndex(
                name: "IX_GenerationRuns_CreatedAt",
                schema: "inference",
                table: "GenerationRuns");
        }
    }
}
