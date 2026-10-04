using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiNexus.BuildingBlocks.Migrations
{
    /// <inheritdoc />
    public partial class GenerationExecutorLeases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ExecutorId",
                schema: "inference",
                table: "GenerationRuns",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LeaseExpiresAt",
                schema: "inference",
                table: "GenerationRuns",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GenerationRuns_ActiveOwnerId_LeaseExpiresAt",
                schema: "inference",
                table: "GenerationRuns",
                columns: new[] { "ActiveOwnerId", "LeaseExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GenerationRuns_ActiveOwnerId_LeaseExpiresAt",
                schema: "inference",
                table: "GenerationRuns");

            migrationBuilder.DropColumn(
                name: "ExecutorId",
                schema: "inference",
                table: "GenerationRuns");

            migrationBuilder.DropColumn(
                name: "LeaseExpiresAt",
                schema: "inference",
                table: "GenerationRuns");
        }
    }
}
