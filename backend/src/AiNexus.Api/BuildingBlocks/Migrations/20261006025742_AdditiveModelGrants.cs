using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiNexus.BuildingBlocks.Migrations
{
    /// <inheritdoc />
    public partial class AdditiveModelGrants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "DailyTokenLimitsJson",
                schema: "access",
                table: "UserModelPolicies",
                type: "nvarchar(max)",
                maxLength: 8000,
                nullable: true,
                comment: "各模型每日輸入加輸出 token 上限 JSON；授權群組取最高值、留空不限，個人覆寫優先，UTC 午夜重設。",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldMaxLength: 8000,
                oldNullable: true,
                oldComment: "各模型每日輸入加輸出 token 上限 JSON；個人覆寫優先，群組取最低值，UTC 午夜重設。");

            migrationBuilder.AlterColumn<string>(
                name: "AllowedModelsJson",
                schema: "access",
                table: "UserModelPolicies",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true,
                comment: "模型白名單 JSON；群組取聯集，空值授予全部、空陣列不授權；個人白名單再限縮。",
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000,
                oldNullable: true,
                oldComment: "模型白名單 JSON；空值不增加限制，空陣列禁止生成。");

            migrationBuilder.AlterColumn<string>(
                name: "DailyTokenLimitsJson",
                schema: "access",
                table: "GroupModelPolicies",
                type: "nvarchar(max)",
                maxLength: 8000,
                nullable: true,
                comment: "各模型每日輸入加輸出 token 上限 JSON；授權群組取最高值、留空不限，個人覆寫優先，UTC 午夜重設。",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldMaxLength: 8000,
                oldNullable: true,
                oldComment: "各模型每日輸入加輸出 token 上限 JSON；個人覆寫優先，群組取最低值，UTC 午夜重設。");

            migrationBuilder.AlterColumn<string>(
                name: "AllowedModelsJson",
                schema: "access",
                table: "GroupModelPolicies",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true,
                comment: "模型白名單 JSON；群組取聯集，空值授予全部、空陣列不授權；個人白名單再限縮。",
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000,
                oldNullable: true,
                oldComment: "模型白名單 JSON；空值不增加限制，空陣列禁止生成。");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "DailyTokenLimitsJson",
                schema: "access",
                table: "UserModelPolicies",
                type: "nvarchar(max)",
                maxLength: 8000,
                nullable: true,
                comment: "各模型每日輸入加輸出 token 上限 JSON；個人覆寫優先，群組取最低值，UTC 午夜重設。",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldMaxLength: 8000,
                oldNullable: true,
                oldComment: "各模型每日輸入加輸出 token 上限 JSON；授權群組取最高值、留空不限，個人覆寫優先，UTC 午夜重設。");

            migrationBuilder.AlterColumn<string>(
                name: "AllowedModelsJson",
                schema: "access",
                table: "UserModelPolicies",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true,
                comment: "模型白名單 JSON；空值不增加限制，空陣列禁止生成。",
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000,
                oldNullable: true,
                oldComment: "模型白名單 JSON；群組取聯集，空值授予全部、空陣列不授權；個人白名單再限縮。");

            migrationBuilder.AlterColumn<string>(
                name: "DailyTokenLimitsJson",
                schema: "access",
                table: "GroupModelPolicies",
                type: "nvarchar(max)",
                maxLength: 8000,
                nullable: true,
                comment: "各模型每日輸入加輸出 token 上限 JSON；個人覆寫優先，群組取最低值，UTC 午夜重設。",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldMaxLength: 8000,
                oldNullable: true,
                oldComment: "各模型每日輸入加輸出 token 上限 JSON；授權群組取最高值、留空不限，個人覆寫優先，UTC 午夜重設。");

            migrationBuilder.AlterColumn<string>(
                name: "AllowedModelsJson",
                schema: "access",
                table: "GroupModelPolicies",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true,
                comment: "模型白名單 JSON；空值不增加限制，空陣列禁止生成。",
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000,
                oldNullable: true,
                oldComment: "模型白名單 JSON；群組取聯集，空值授予全部、空陣列不授權；個人白名單再限縮。");
        }
    }
}
