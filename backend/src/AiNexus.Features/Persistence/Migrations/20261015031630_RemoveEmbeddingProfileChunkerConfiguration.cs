using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiNexus.Features.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveEmbeddingProfileChunkerConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChunkerConfiguration",
                schema: "knowledge",
                table: "EmbeddingProfiles");

            migrationBuilder.AlterTable(
                name: "EmbeddingProfiles",
                schema: "knowledge",
                comment: "向量空間的不可變快照，切段規則納入 Key 指紋；同時最多一個 active。",
                oldComment: "向量空間及切段規則的不可變快照；同時最多一個 active。");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "EmbeddingProfiles",
                schema: "knowledge",
                comment: "向量空間及切段規則的不可變快照；同時最多一個 active。",
                oldComment: "向量空間的不可變快照，切段規則納入 Key 指紋；同時最多一個 active。");

            migrationBuilder.AddColumn<string>(
                name: "ChunkerConfiguration",
                schema: "knowledge",
                table: "EmbeddingProfiles",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "",
                comment: "切段器版本及 token／重疊參數快照；重建期間保留舊版本。");
        }
    }
}
