using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiNexus.BuildingBlocks.Migrations
{
    /// <inheritdoc />
    public partial class ReadingDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "SidebarWidth",
                schema: "identity",
                table: "UserPreferences",
                type: "int",
                nullable: false,
                defaultValue: 240,
                comment: "側欄寬度偏好。",
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 264,
                oldComment: "側欄寬度偏好。");

            migrationBuilder.AlterColumn<double>(
                name: "ReadingLineHeight",
                schema: "identity",
                table: "UserPreferences",
                type: "float",
                nullable: false,
                defaultValue: 1.2,
                comment: "對話閱讀行高倍率。",
                oldClrType: typeof(double),
                oldType: "float",
                oldDefaultValue: 1.8,
                oldComment: "對話閱讀行高倍率。");

            migrationBuilder.AlterColumn<int>(
                name: "ReadingFontSize",
                schema: "identity",
                table: "UserPreferences",
                type: "int",
                nullable: false,
                defaultValue: 15,
                comment: "對話文字大小，以 CSS px 的偏好值記錄。",
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 17,
                oldComment: "對話文字大小，以 CSS px 的偏好值記錄。");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "SidebarWidth",
                schema: "identity",
                table: "UserPreferences",
                type: "int",
                nullable: false,
                defaultValue: 264,
                comment: "側欄寬度偏好。",
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 240,
                oldComment: "側欄寬度偏好。");

            migrationBuilder.AlterColumn<double>(
                name: "ReadingLineHeight",
                schema: "identity",
                table: "UserPreferences",
                type: "float",
                nullable: false,
                defaultValue: 1.8,
                comment: "對話閱讀行高倍率。",
                oldClrType: typeof(double),
                oldType: "float",
                oldDefaultValue: 1.2,
                oldComment: "對話閱讀行高倍率。");

            migrationBuilder.AlterColumn<int>(
                name: "ReadingFontSize",
                schema: "identity",
                table: "UserPreferences",
                type: "int",
                nullable: false,
                defaultValue: 17,
                comment: "對話文字大小，以 CSS px 的偏好值記錄。",
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 15,
                oldComment: "對話文字大小，以 CSS px 的偏好值記錄。");
        }
    }
}
