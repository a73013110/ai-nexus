using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiNexus.Features.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SingleChunkLayout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Chunks_EmbeddingProfiles_ProfileId",
                schema: "knowledge",
                table: "Chunks");

            migrationBuilder.DropIndex(
                name: "IX_Chunks_ProfileId_DocumentId",
                schema: "knowledge",
                table: "Chunks");

            migrationBuilder.DropColumn(
                name: "ProfileId",
                schema: "knowledge",
                table: "Chunks");

            migrationBuilder.AlterTable(
                name: "Chunks",
                schema: "knowledge",
                comment: "結構化檢索片段、頁碼與內容指紋；查詢先套用資料 ACL。",
                oldComment: "結構化檢索片段與 profile 專屬切段版本；查詢先套用資料 ACL。");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "Chunks",
                schema: "knowledge",
                comment: "結構化檢索片段與 profile 專屬切段版本；查詢先套用資料 ACL。",
                oldComment: "結構化檢索片段、頁碼與內容指紋；查詢先套用資料 ACL。");

            migrationBuilder.AddColumn<int>(
                name: "ProfileId",
                schema: "knowledge",
                table: "Chunks",
                type: "int",
                nullable: false,
                defaultValue: 0,
                comment: "向量空間及切段版本的 EmbeddingProfiles 外鍵。");

            migrationBuilder.CreateIndex(
                name: "IX_Chunks_ProfileId_DocumentId",
                schema: "knowledge",
                table: "Chunks",
                columns: new[] { "ProfileId", "DocumentId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Chunks_EmbeddingProfiles_ProfileId",
                schema: "knowledge",
                table: "Chunks",
                column: "ProfileId",
                principalSchema: "knowledge",
                principalTable: "EmbeddingProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
