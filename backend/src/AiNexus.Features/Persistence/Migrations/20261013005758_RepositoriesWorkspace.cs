using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiNexus.Features.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RepositoriesWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CommentUrl",
                schema: "repositories",
                table: "RepositoryReviews",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                comment: "報告貼到 Pull Request 的留言網址；空值表示尚未張貼。");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CommentedAt",
                schema: "repositories",
                table: "RepositoryReviews",
                type: "datetimeoffset",
                nullable: true,
                comment: "報告貼到 Pull Request 的時間。");

            migrationBuilder.AddColumn<int>(
                name: "PullRequest",
                schema: "repositories",
                table: "RepositoryReviews",
                type: "int",
                nullable: true,
                comment: "來源 Pull Request 編號；空值表示直接選擇的 commit 或區間。");

            migrationBuilder.CreateTable(
                name: "RepositoryFolderImports",
                schema: "repositories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料的主鍵識別碼。"),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。"),
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "關聯背景工作識別碼。"),
                    BaseUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false, comment: "Gitea 連線主機位址。"),
                    Repository = table.Column<string>(type: "nvarchar(201)", maxLength: 201, nullable: false, comment: "Gitea repository 的 owner/name 識別。"),
                    Commit = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, comment: "匯入當時固定的 commit SHA。"),
                    Path = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false, comment: "repository 內的資料夾路徑；空字串為根目錄。"),
                    CollectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "關聯知識庫的識別碼。"),
                    PathsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 600000, nullable: false, comment: "建立時選定的檔案路徑 JSON 陣列。"),
                    Imported = table.Column<int>(type: "int", nullable: false, comment: "已匯入或沿用的檔案數。"),
                    Skipped = table.Column<int>(type: "int", nullable: false, comment: "因非文字或超過上限而略過的檔案數。"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "資料建立時間，採 UTC offset。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositoryFolderImports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepositoryFolderImports_BackgroundJobs_JobId",
                        column: x => x.JobId,
                        principalSchema: "jobs",
                        principalTable: "BackgroundJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RepositoryFolderImports_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "程式庫資料夾匯入知識庫的固定版本、檔案清單與背景工作。");

            migrationBuilder.CreateTable(
                name: "RepositoryPins",
                schema: "repositories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料的主鍵識別碼。"),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "資料擁有者／有效操作身分的 Users 主鍵；用於私人資料隔離。"),
                    BaseUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false, comment: "Gitea 連線主機位址。"),
                    Repository = table.Column<string>(type: "nvarchar(201)", maxLength: 201, nullable: false, comment: "Gitea repository 的 owner/name 識別。"),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false, comment: "釘選當時的程式庫說明。"),
                    Private = table.Column<bool>(type: "bit", nullable: false, comment: "釘選當時是否為私人程式庫。"),
                    PinnedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "釘選的時間。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositoryPins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepositoryPins_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "使用者釘選的 Gitea 程式庫；名稱與說明為釘選當時的快照。");

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryFolderImports_JobId",
                schema: "repositories",
                table: "RepositoryFolderImports",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryFolderImports_OwnerId_CreatedAt",
                schema: "repositories",
                table: "RepositoryFolderImports",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryPins_OwnerId_BaseUrl_Repository",
                schema: "repositories",
                table: "RepositoryPins",
                columns: new[] { "OwnerId", "BaseUrl", "Repository" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RepositoryFolderImports",
                schema: "repositories");

            migrationBuilder.DropTable(
                name: "RepositoryPins",
                schema: "repositories");

            migrationBuilder.DropColumn(
                name: "CommentUrl",
                schema: "repositories",
                table: "RepositoryReviews");

            migrationBuilder.DropColumn(
                name: "CommentedAt",
                schema: "repositories",
                table: "RepositoryReviews");

            migrationBuilder.DropColumn(
                name: "PullRequest",
                schema: "repositories",
                table: "RepositoryReviews");
        }
    }
}
