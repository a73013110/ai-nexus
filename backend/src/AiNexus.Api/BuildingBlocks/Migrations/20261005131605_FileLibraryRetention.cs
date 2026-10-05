using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiNexus.BuildingBlocks.Migrations
{
    /// <inheritdoc />
    public partial class FileLibraryRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "InLibrary",
                schema: "attachments",
                table: "Attachments",
                type: "bit",
                nullable: false,
                defaultValue: false,
                comment: "是否由個人檔案庫獨立保留原檔；移除對話或知識索引不會刪除保留的檔案。");

            // Upgrade existing history and knowledge originals without copying their binary data.
            migrationBuilder.Sql("""
                UPDATE a SET [InLibrary] = 1 FROM [attachments].[Attachments] a
                WHERE EXISTS (SELECT 1 FROM [attachments].[MessageAttachments] m WHERE m.[AttachmentId] = a.[Id])
                   OR EXISTS (SELECT 1 FROM [attachments].[ResourceAttachments] r WHERE r.[AttachmentId] = a.[Id]);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_OwnerId_InLibrary_CreatedAt_Id",
                schema: "attachments",
                table: "Attachments",
                columns: new[] { "OwnerId", "InLibrary", "CreatedAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Attachments_OwnerId_InLibrary_CreatedAt_Id",
                schema: "attachments",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "InLibrary",
                schema: "attachments",
                table: "Attachments");
        }
    }
}
