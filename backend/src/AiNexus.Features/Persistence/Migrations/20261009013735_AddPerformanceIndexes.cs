using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiNexus.Features.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_GenerationRuns_OwnerId_CreatedAt",
                schema: "inference",
                table: "GenerationRuns",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobs_SubjectId",
                schema: "operations",
                table: "BackgroundJobs",
                column: "SubjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GenerationRuns_OwnerId_CreatedAt",
                schema: "inference",
                table: "GenerationRuns");

            migrationBuilder.DropIndex(
                name: "IX_BackgroundJobs_SubjectId",
                schema: "operations",
                table: "BackgroundJobs");
        }
    }
}
