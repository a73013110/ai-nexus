using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiNexus.BuildingBlocks.Migrations
{
    /// <inheritdoc />
    public partial class CitationPageRanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EndPage",
                schema: "knowledge",
                table: "MessageCitations",
                type: "int",
                nullable: false,
                defaultValue: 0,
                comment: "片段結束的原始文件頁碼。");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndPage",
                schema: "knowledge",
                table: "MessageCitations");
        }
    }
}
