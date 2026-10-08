using System;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiNexus.Features.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VectorRetrievalProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM [knowledge].[Chunks];
                UPDATE [knowledge].[Documents] SET [Status] = 'reindex', [ChunkCount] = 0 WHERE [CollectionId] IS NOT NULL AND [IsDeleted] = 0;
                IF COL_LENGTH('knowledge.Chunks', 'EmbeddingVector') IS NOT NULL ALTER TABLE [knowledge].[Chunks] DROP COLUMN [EmbeddingVector];
                IF COL_LENGTH('knowledge.Chunks', 'EmbeddingVector1024') IS NOT NULL ALTER TABLE [knowledge].[Chunks] DROP COLUMN [EmbeddingVector1024];
                """);
            migrationBuilder.DropIndex(
                name: "IX_Documents_CollectionId_Status",
                schema: "knowledge",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "EmbeddingProfile",
                schema: "knowledge",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "EmbeddingJson",
                schema: "knowledge",
                table: "Chunks");

            migrationBuilder.DropColumn(
                name: "EmbeddingProfile",
                schema: "knowledge",
                table: "Chunks");

            migrationBuilder.DropColumn(
                name: "PageNumber",
                schema: "knowledge",
                table: "Chunks");

            migrationBuilder.AlterTable(
                name: "Chunks",
                schema: "knowledge",
                comment: "結構化檢索片段與 profile 專屬切段版本；查詢先套用資料 ACL。",
                oldComment: "知識檢索片段、頁碼、摘要與向量；查詢先套用資料 ACL。");

            migrationBuilder.AlterColumn<string>(
                name: "Text",
                schema: "knowledge",
                table: "Chunks",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                comment: "文件頁面／片段的擷取文字。",
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldComment: "文件頁面／片段的擷取文字。");

            migrationBuilder.AddColumn<byte[]>(
                name: "ContentHash",
                schema: "knowledge",
                table: "Chunks",
                type: "binary(32)",
                nullable: false,
                defaultValue: new byte[0],
                comment: "實際向量輸入（文件名稱、標題路徑與本文）的 SHA-256。");

            migrationBuilder.AddColumn<int>(
                name: "EndPage",
                schema: "knowledge",
                table: "Chunks",
                type: "int",
                nullable: false,
                defaultValue: 0,
                comment: "片段結束的原始文件頁碼。");

            migrationBuilder.AddColumn<string>(
                name: "HeadingPath",
                schema: "knowledge",
                table: "Chunks",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: false,
                defaultValue: "",
                comment: "由標題階層組成的結構路徑。");

            migrationBuilder.AddColumn<int>(
                name: "ProfileId",
                schema: "knowledge",
                table: "Chunks",
                type: "int",
                nullable: false,
                defaultValue: 0,
                comment: "向量空間及切段版本的 EmbeddingProfiles 外鍵。");

            migrationBuilder.AddColumn<int>(
                name: "SearchId",
                schema: "knowledge",
                table: "Chunks",
                type: "int",
                nullable: false,
                defaultValue: 0,
                comment: "全文索引使用的整數唯一鍵；保留未來 ANN 映射。")
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddColumn<int>(
                name: "StartPage",
                schema: "knowledge",
                table: "Chunks",
                type: "int",
                nullable: false,
                defaultValue: 0,
                comment: "片段開始的原始文件頁碼。");

            migrationBuilder.AddColumn<int>(
                name: "TokenEstimate",
                schema: "knowledge",
                table: "Chunks",
                type: "int",
                nullable: false,
                defaultValue: 0,
                comment: "依 CJK 與其他字元比例估算的片段 token 數。");

            migrationBuilder.CreateTable(
                name: "EmbeddingProfiles",
                schema: "knowledge",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false, comment: "資料的主鍵識別碼。")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, comment: "供應商、模型、維度及輸入／切段規則的唯一指紋。"),
                    Provider = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, comment: "模型或搜尋服務供應商識別碼。"),
                    Model = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false, comment: "建立此向量空間時的模型識別碼。"),
                    Dimensions = table.Column<int>(type: "int", nullable: false, comment: "向量維度，限已建立資料表的 allowlist。"),
                    InputFormat = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, comment: "查詢輸入格式 plain 或 qwen-query。"),
                    QueryInstruction = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false, comment: "qwen-query 的檢索任務指令快照。"),
                    Revision = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, comment: "外部來源或 repository 的固定版本識別。"),
                    ChunkerConfiguration = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false, comment: "切段器版本及 token／重疊參數快照；重建期間保留舊版本。"),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, comment: "業務執行狀態。"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, comment: "資料建立時間，採 UTC offset。"),
                    ActivatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "此 profile 完整覆蓋並切換為 active 的 UTC 時間。"),
                    RetiredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true, comment: "此 profile 退役的 UTC 時間；作為保留期清理依據。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmbeddingProfiles", x => x.Id);
                },
                comment: "向量空間及切段規則的不可變快照；同時最多一個 active。");

            migrationBuilder.CreateTable(
                name: "ChunkEmbeddings1024",
                schema: "knowledge",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false, comment: "資料的主鍵識別碼。")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChunkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "向量對應的結構化片段 Guid 識別碼。"),
                    ProfileId = table.Column<int>(type: "int", nullable: false, comment: "向量空間及切段版本的 EmbeddingProfiles 外鍵。"),
                    ContentHash = table.Column<byte[]>(type: "binary(32)", nullable: false, comment: "實際向量輸入（文件名稱、標題路徑與本文）的 SHA-256。"),
                    Vector = table.Column<SqlVector<float>>(type: "vector(1024)", nullable: false, comment: "L2 正規化的 float32 向量；SQL Server 使用 VECTOR 型別。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChunkEmbeddings1024", x => x.Id)
                        .Annotation("SqlServer:Clustered", true);
                    table.ForeignKey(
                        name: "FK_ChunkEmbeddings1024_Chunks_ChunkId",
                        column: x => x.ChunkId,
                        principalSchema: "knowledge",
                        principalTable: "Chunks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChunkEmbeddings1024_EmbeddingProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalSchema: "knowledge",
                        principalTable: "EmbeddingProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "1024 維原生向量、片段關聯與 profile 內容快取。");

            migrationBuilder.CreateTable(
                name: "ChunkEmbeddings768",
                schema: "knowledge",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false, comment: "資料的主鍵識別碼。")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChunkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, comment: "向量對應的結構化片段 Guid 識別碼。"),
                    ProfileId = table.Column<int>(type: "int", nullable: false, comment: "向量空間及切段版本的 EmbeddingProfiles 外鍵。"),
                    ContentHash = table.Column<byte[]>(type: "binary(32)", nullable: false, comment: "實際向量輸入（文件名稱、標題路徑與本文）的 SHA-256。"),
                    Vector = table.Column<SqlVector<float>>(type: "vector(768)", nullable: false, comment: "L2 正規化的 float32 向量；SQL Server 使用 VECTOR 型別。")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChunkEmbeddings768", x => x.Id)
                        .Annotation("SqlServer:Clustered", true);
                    table.ForeignKey(
                        name: "FK_ChunkEmbeddings768_Chunks_ChunkId",
                        column: x => x.ChunkId,
                        principalSchema: "knowledge",
                        principalTable: "Chunks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChunkEmbeddings768_EmbeddingProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalSchema: "knowledge",
                        principalTable: "EmbeddingProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "768 維原生向量、片段關聯與 profile 內容快取。");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_CollectionId_Status_IsDeleted",
                schema: "knowledge",
                table: "Documents",
                columns: new[] { "CollectionId", "Status", "IsDeleted" })
                .Annotation("SqlServer:Include", new[] { "Id", "FileName", "ChunkCount" });

            migrationBuilder.CreateIndex(
                name: "IX_Chunks_ProfileId_DocumentId",
                schema: "knowledge",
                table: "Chunks",
                columns: new[] { "ProfileId", "DocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_Chunks_SearchId",
                schema: "knowledge",
                table: "Chunks",
                column: "SearchId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChunkEmbeddings1024_ChunkId",
                schema: "knowledge",
                table: "ChunkEmbeddings1024",
                column: "ChunkId");

            migrationBuilder.CreateIndex(
                name: "IX_ChunkEmbeddings1024_ProfileId_ChunkId",
                schema: "knowledge",
                table: "ChunkEmbeddings1024",
                columns: new[] { "ProfileId", "ChunkId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChunkEmbeddings1024_ProfileId_ContentHash",
                schema: "knowledge",
                table: "ChunkEmbeddings1024",
                columns: new[] { "ProfileId", "ContentHash" });

            migrationBuilder.CreateIndex(
                name: "IX_ChunkEmbeddings768_ChunkId",
                schema: "knowledge",
                table: "ChunkEmbeddings768",
                column: "ChunkId");

            migrationBuilder.CreateIndex(
                name: "IX_ChunkEmbeddings768_ProfileId_ChunkId",
                schema: "knowledge",
                table: "ChunkEmbeddings768",
                columns: new[] { "ProfileId", "ChunkId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChunkEmbeddings768_ProfileId_ContentHash",
                schema: "knowledge",
                table: "ChunkEmbeddings768",
                columns: new[] { "ProfileId", "ContentHash" });

            migrationBuilder.CreateIndex(
                name: "IX_EmbeddingProfiles_Key",
                schema: "knowledge",
                table: "EmbeddingProfiles",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmbeddingProfiles_Status",
                schema: "knowledge",
                table: "EmbeddingProfiles",
                column: "Status",
                unique: true,
                filter: "[Status] = 'active'");

            migrationBuilder.AddForeignKey(
                name: "FK_Chunks_EmbeddingProfiles_ProfileId",
                schema: "knowledge",
                table: "Chunks",
                column: "ProfileId",
                principalSchema: "knowledge",
                principalTable: "EmbeddingProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("""
                IF CONVERT(int, SERVERPROPERTY('IsFullTextInstalled')) = 1
                    AND EXISTS (SELECT 1 FROM sys.fulltext_languages WHERE lcid = 1028)
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'KnowledgeSearch')
                        EXEC('CREATE FULLTEXT CATALOG [KnowledgeSearch]');
                    IF NOT EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('knowledge.Chunks'))
                        EXEC('CREATE FULLTEXT INDEX ON [knowledge].[Chunks] ([Text] LANGUAGE 1028, [HeadingPath] LANGUAGE 1028) KEY INDEX [IX_Chunks_SearchId] ON [KnowledgeSearch] WITH CHANGE_TRACKING AUTO');
                END
                ELSE RAISERROR(N'知識全文索引未建立：未安裝全文元件或繁體中文 1028 斷詞器；執行期會明確回報 vector 模式。', 10, 1) WITH NOWAIT;
                """, suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('knowledge.Chunks')) DROP FULLTEXT INDEX ON [knowledge].[Chunks];", suppressTransaction: true);
            migrationBuilder.Sql("DELETE FROM [knowledge].[Chunks];");
            migrationBuilder.DropForeignKey(
                name: "FK_Chunks_EmbeddingProfiles_ProfileId",
                schema: "knowledge",
                table: "Chunks");

            migrationBuilder.DropTable(
                name: "ChunkEmbeddings1024",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "ChunkEmbeddings768",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "EmbeddingProfiles",
                schema: "knowledge");

            migrationBuilder.DropIndex(
                name: "IX_Documents_CollectionId_Status_IsDeleted",
                schema: "knowledge",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Chunks_ProfileId_DocumentId",
                schema: "knowledge",
                table: "Chunks");

            migrationBuilder.DropIndex(
                name: "IX_Chunks_SearchId",
                schema: "knowledge",
                table: "Chunks");

            migrationBuilder.DropColumn(
                name: "ContentHash",
                schema: "knowledge",
                table: "Chunks");

            migrationBuilder.DropColumn(
                name: "EndPage",
                schema: "knowledge",
                table: "Chunks");

            migrationBuilder.DropColumn(
                name: "HeadingPath",
                schema: "knowledge",
                table: "Chunks");

            migrationBuilder.DropColumn(
                name: "ProfileId",
                schema: "knowledge",
                table: "Chunks");

            migrationBuilder.DropColumn(
                name: "SearchId",
                schema: "knowledge",
                table: "Chunks");

            migrationBuilder.DropColumn(
                name: "StartPage",
                schema: "knowledge",
                table: "Chunks");

            migrationBuilder.DropColumn(
                name: "TokenEstimate",
                schema: "knowledge",
                table: "Chunks");

            migrationBuilder.AlterTable(
                name: "Chunks",
                schema: "knowledge",
                comment: "知識檢索片段、頁碼、摘要與向量；查詢先套用資料 ACL。",
                oldComment: "結構化檢索片段與 profile 專屬切段版本；查詢先套用資料 ACL。");

            migrationBuilder.AddColumn<string>(
                name: "EmbeddingProfile",
                schema: "knowledge",
                table: "Documents",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                comment: "向量模型、維度與前處理的版本指紋；不混用不同 profile。");

            migrationBuilder.AlterColumn<string>(
                name: "Text",
                schema: "knowledge",
                table: "Chunks",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                comment: "文件頁面／片段的擷取文字。",
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000,
                oldComment: "文件頁面／片段的擷取文字。");

            migrationBuilder.AddColumn<string>(
                name: "EmbeddingJson",
                schema: "knowledge",
                table: "Chunks",
                type: "nvarchar(max)",
                nullable: true,
                comment: "正規化向量的 JSON 表示；與 embedding profile 一起判斷相容性。");

            migrationBuilder.AddColumn<string>(
                name: "EmbeddingProfile",
                schema: "knowledge",
                table: "Chunks",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                comment: "向量模型、維度與前處理的版本指紋；不混用不同 profile。");

            migrationBuilder.AddColumn<int>(
                name: "PageNumber",
                schema: "knowledge",
                table: "Chunks",
                type: "int",
                nullable: false,
                defaultValue: 0,
                comment: "文件頁碼，從 1 開始。");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_CollectionId_Status",
                schema: "knowledge",
                table: "Documents",
                columns: new[] { "CollectionId", "Status" });
            migrationBuilder.Sql("ALTER TABLE [knowledge].[Chunks] ADD [EmbeddingVector] VECTOR(768) NULL, [EmbeddingVector1024] VECTOR(1024) NULL;");
        }
    }
}
