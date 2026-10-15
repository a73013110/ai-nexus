using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiNexus.Features.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                schema: "repositories",
                table: "RepositoryReviews",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "",
                comment: "Review 目的：review（程式碼檢閱）或 summary（變更摘要）；與快照相同，列表不必讀取快照。");

            // Existing reviews: the purpose frozen in the snapshot; snapshots of other versions were always shown as review.
            migrationBuilder.DataSql("""
                UPDATE [repositories].[RepositoryReviews]
                SET [Purpose] = CASE WHEN ISJSON([SnapshotJson]) = 1
                    THEN CASE WHEN JSON_VALUE([SnapshotJson], '$.Version') = '3' AND JSON_VALUE([SnapshotJson], '$.Purpose') IN (N'review', N'summary')
                        THEN JSON_VALUE([SnapshotJson], '$.Purpose') ELSE N'review' END
                    ELSE N'review' END;
                """, "repositories.RepositoryReviews");

            migrationBuilder.CreateIndex(
                name: "IX_RunEvents_CreatedAt",
                schema: "inference",
                table: "RunEvents",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Resources_Kind_UpdatedAt",
                schema: "collaboration",
                table: "Resources",
                columns: new[] { "Kind", "UpdatedAt" })
                .Annotation("SqlServer:Include", new[] { "OwnerId", "ParentId", "IsDeleted" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RunEvents_CreatedAt",
                schema: "inference",
                table: "RunEvents");

            migrationBuilder.DropIndex(
                name: "IX_Resources_Kind_UpdatedAt",
                schema: "collaboration",
                table: "Resources");

            migrationBuilder.DropColumn(
                name: "Purpose",
                schema: "repositories",
                table: "RepositoryReviews");
        }
    }
}
