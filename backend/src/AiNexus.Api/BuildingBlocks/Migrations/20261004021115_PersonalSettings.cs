using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiNexus.BuildingBlocks.Migrations
{
    /// <inheritdoc />
    public partial class PersonalSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoFollow",
                schema: "identity",
                table: "UserPreferences",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultReasoningEffort",
                schema: "identity",
                table: "UserPreferences",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "auto");

            migrationBuilder.AddColumn<string>(
                name: "Density",
                schema: "identity",
                table: "UserPreferences",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "comfortable");

            migrationBuilder.AddColumn<bool>(
                name: "EnterToSend",
                schema: "identity",
                table: "UserPreferences",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyOnCompletion",
                schema: "identity",
                table: "UserPreferences",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ReadingFontSize",
                schema: "identity",
                table: "UserPreferences",
                type: "int",
                nullable: false,
                defaultValue: 17);

            migrationBuilder.AddColumn<double>(
                name: "ReadingLineHeight",
                schema: "identity",
                table: "UserPreferences",
                type: "float",
                nullable: false,
                defaultValue: 1.8);

            migrationBuilder.AddColumn<string>(
                name: "ReadingWidth",
                schema: "identity",
                table: "UserPreferences",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "standard");

            migrationBuilder.AddColumn<bool>(
                name: "SaveLocalDrafts",
                schema: "identity",
                table: "UserPreferences",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "SidebarWidth",
                schema: "identity",
                table: "UserPreferences",
                type: "int",
                nullable: false,
                defaultValue: 264);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoFollow",
                schema: "identity",
                table: "UserPreferences");

            migrationBuilder.DropColumn(
                name: "DefaultReasoningEffort",
                schema: "identity",
                table: "UserPreferences");

            migrationBuilder.DropColumn(
                name: "Density",
                schema: "identity",
                table: "UserPreferences");

            migrationBuilder.DropColumn(
                name: "EnterToSend",
                schema: "identity",
                table: "UserPreferences");

            migrationBuilder.DropColumn(
                name: "NotifyOnCompletion",
                schema: "identity",
                table: "UserPreferences");

            migrationBuilder.DropColumn(
                name: "ReadingFontSize",
                schema: "identity",
                table: "UserPreferences");

            migrationBuilder.DropColumn(
                name: "ReadingLineHeight",
                schema: "identity",
                table: "UserPreferences");

            migrationBuilder.DropColumn(
                name: "ReadingWidth",
                schema: "identity",
                table: "UserPreferences");

            migrationBuilder.DropColumn(
                name: "SaveLocalDrafts",
                schema: "identity",
                table: "UserPreferences");

            migrationBuilder.DropColumn(
                name: "SidebarWidth",
                schema: "identity",
                table: "UserPreferences");
        }
    }
}
