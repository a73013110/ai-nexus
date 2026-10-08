using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiNexus.Features.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PerModelTokenBudgets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DailyRequestLimit",
                schema: "access",
                table: "GroupModelPolicies");

            migrationBuilder.AlterTable(
                name: "GroupModelPolicies",
                schema: "access",
                comment: "功能群組的模型白名單、各模型每日 token 及附件空間限制。",
                oldComment: "功能群組的模型白名單、每日生成及附件空間限制。");

            migrationBuilder.AddColumn<long>(
                name: "ReservedTokens",
                schema: "inference",
                table: "ModelInvocations",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                comment: "生成預留的保守輸入加最大輸出 token；執行中或缺失 usage 時占用配額，未執行即取消釋放。");

            migrationBuilder.AddColumn<string>(
                name: "DailyTokenLimitsJson",
                schema: "access",
                table: "GroupModelPolicies",
                type: "nvarchar(max)",
                maxLength: 8000,
                nullable: true,
                comment: "各模型每日輸入加輸出 token 上限 JSON；個人覆寫優先，群組取最低值，UTC 午夜重設。");

            migrationBuilder.AddColumn<long>(
                name: "ReservedTokens",
                schema: "inference",
                table: "GenerationRuns",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                comment: "生成預留的保守輸入加最大輸出 token；執行中或缺失 usage 時占用配額，未執行即取消釋放。");

            migrationBuilder.CreateTable(
                name: "UserModelPolicies",
                schema: "access",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "關聯使用者的 Users 主鍵。"),
                    AllowedModelsJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true, comment: "模型白名單 JSON；空值不增加限制，空陣列禁止生成。"),
                    DailyTokenLimitsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true, comment: "各模型每日輸入加輸出 token 上限 JSON；個人覆寫優先，群組取最低值，UTC 午夜重設。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserModelPolicies", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserModelPolicies_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "使用者的模型白名單與各模型每日 token 覆寫政策。");

            migrationBuilder.Sql("UPDATE [access].[Features] SET [Name] = N'成果' WHERE [Id] = N'artifacts' AND [Name] = N'成果文件';");

            migrationBuilder.Sql("UPDATE [access].[Features] SET [Name] = N'對話' WHERE [Id] = N'chat' AND [Name] = N'AI 對話';");

            migrationBuilder.Sql("UPDATE [access].[Features] SET [Name] = N'來源' WHERE [Id] = N'integrations' AND [Name] = N'系統整合';");

            migrationBuilder.Sql("UPDATE [access].[Features] SET [Name] = N'知識' WHERE [Id] = N'knowledge' AND [Name] = N'知識庫';");

            migrationBuilder.Sql("UPDATE [access].[Features] SET [Name] = N'評測' WHERE [Id] = N'quality' AND [Name] = N'品質評測';");

            migrationBuilder.Sql("UPDATE [access].[Features] SET [Name] = N'任務' WHERE [Id] = N'tasks' AND [Name] = N'背景任務';");

            migrationBuilder.InsertData(
                schema: "access",
                table: "Features",
                columns: new[] { "Id", "Enabled", "Name", "Route", "SortOrder" },
                values: new object[] { "files", true, "檔案庫", "/files", 15 });

            migrationBuilder.InsertData(
                schema: "access",
                table: "RoleGroupFeatures",
                columns: new[] { "FeatureId", "GroupId" },
                values: new object[] { "files", "workspace" });

            // The former library route inherited access from any attachment-using feature.
            migrationBuilder.Sql("""
                INSERT INTO [access].[RoleGroupFeatures] ([FeatureId], [GroupId])
                SELECT DISTINCT N'files', grants.[GroupId] FROM [access].[RoleGroupFeatures] grants
                WHERE grants.[FeatureId] IN (N'chat', N'knowledge', N'projects')
                  AND NOT EXISTS (SELECT 1 FROM [access].[RoleGroupFeatures] existing WHERE existing.[GroupId] = grants.[GroupId] AND existing.[FeatureId] = N'files');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserModelPolicies",
                schema: "access");

            migrationBuilder.Sql("DELETE FROM [access].[RoleGroupFeatures] WHERE [FeatureId] = N'files';");

            migrationBuilder.DeleteData(
                schema: "access",
                table: "Features",
                keyColumn: "Id",
                keyValue: "files");

            migrationBuilder.DropColumn(
                name: "ReservedTokens",
                schema: "inference",
                table: "ModelInvocations");

            migrationBuilder.DropColumn(
                name: "DailyTokenLimitsJson",
                schema: "access",
                table: "GroupModelPolicies");

            migrationBuilder.DropColumn(
                name: "ReservedTokens",
                schema: "inference",
                table: "GenerationRuns");

            migrationBuilder.AlterTable(
                name: "GroupModelPolicies",
                schema: "access",
                comment: "功能群組的模型白名單、每日生成及附件空間限制。",
                oldComment: "功能群組的模型白名單、各模型每日 token 及附件空間限制。");

            migrationBuilder.AddColumn<int>(
                name: "DailyRequestLimit",
                schema: "access",
                table: "GroupModelPolicies",
                type: "int",
                nullable: true,
                comment: "每日生成次數上限；群組限制取最低值，UTC 午夜重設。");

            migrationBuilder.Sql("UPDATE [access].[Features] SET [Name] = N'成果文件' WHERE [Id] = N'artifacts' AND [Name] = N'成果';");

            migrationBuilder.Sql("UPDATE [access].[Features] SET [Name] = N'AI 對話' WHERE [Id] = N'chat' AND [Name] = N'對話';");

            migrationBuilder.Sql("UPDATE [access].[Features] SET [Name] = N'系統整合' WHERE [Id] = N'integrations' AND [Name] = N'來源';");

            migrationBuilder.Sql("UPDATE [access].[Features] SET [Name] = N'知識庫' WHERE [Id] = N'knowledge' AND [Name] = N'知識';");

            migrationBuilder.Sql("UPDATE [access].[Features] SET [Name] = N'品質評測' WHERE [Id] = N'quality' AND [Name] = N'評測';");

            migrationBuilder.Sql("UPDATE [access].[Features] SET [Name] = N'背景任務' WHERE [Id] = N'tasks' AND [Name] = N'任務';");
        }
    }
}
