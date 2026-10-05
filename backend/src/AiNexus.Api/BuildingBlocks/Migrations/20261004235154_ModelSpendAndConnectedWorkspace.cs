using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AiNexus.BuildingBlocks.Migrations
{
    /// <inheritdoc />
    public partial class ModelSpendAndConnectedWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
                migrationBuilder.Sql("IF CONVERT(int,SERVERPROPERTY('ProductMajorVersion')) >= 17 AND COL_LENGTH('knowledge.Chunks','EmbeddingVector1024') IS NULL EXEC(N'ALTER TABLE [knowledge].[Chunks] ADD [EmbeddingVector1024] VECTOR(1024) NULL');");
            migrationBuilder.EnsureSchema(
                name: "workspace");

            migrationBuilder.CreateTable(
                name: "ModelPrices",
                schema: "inference",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ModelId = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    InputPerMillion = table.Column<decimal>(type: "decimal(20,8)", precision: 20, scale: 8, nullable: false),
                    CachedInputPerMillion = table.Column<decimal>(type: "decimal(20,8)", precision: 20, scale: 8, nullable: false),
                    OutputPerMillion = table.Column<decimal>(type: "decimal(20,8)", precision: 20, scale: 8, nullable: false),
                    PerRequest = table.Column<decimal>(type: "decimal(20,8)", precision: 20, scale: 8, nullable: false),
                    RequestCharge = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    EffectiveAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelPrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelPrices_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RepositoryConnections",
                schema: "workspace",
                columns: table => new
                {
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BaseUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Login = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProtectedToken = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: false),
                    ConnectedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositoryConnections", x => x.OwnerId);
                    table.ForeignKey(
                        name: "FK_RepositoryConnections_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RepositoryImports",
                schema: "knowledge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CollectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Repository = table.Column<string>(type: "nvarchar(201)", maxLength: 201, nullable: false),
                    Path = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Commit = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BaseUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositoryImports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepositoryImports_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "knowledge",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WebSearches",
                schema: "inference",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ResultsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebSearches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WebSearches_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ModelCharges",
                schema: "inference",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Provider = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ModelId = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FinishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PriceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    InputPerMillion = table.Column<decimal>(type: "decimal(20,8)", precision: 20, scale: 8, nullable: false),
                    CachedInputPerMillion = table.Column<decimal>(type: "decimal(20,8)", precision: 20, scale: 8, nullable: false),
                    OutputPerMillion = table.Column<decimal>(type: "decimal(20,8)", precision: 20, scale: 8, nullable: false),
                    PerRequest = table.Column<decimal>(type: "decimal(20,8)", precision: 20, scale: 8, nullable: false),
                    RequestCharge = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    InputTokens = table.Column<long>(type: "bigint", nullable: true),
                    CachedInputTokens = table.Column<long>(type: "bigint", nullable: true),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: true),
                    ReasoningTokens = table.Column<long>(type: "bigint", nullable: true),
                    UsageComplete = table.Column<bool>(type: "bit", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(20,8)", precision: 20, scale: 8, nullable: true),
                    State = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelCharges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelCharges_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "conversations",
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ModelCharges_ModelPrices_PriceId",
                        column: x => x.PriceId,
                        principalSchema: "inference",
                        principalTable: "ModelPrices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ModelCharges_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "access",
                table: "Features",
                columns: new[] { "Id", "Enabled", "Name", "Route", "SortOrder" },
                values: new object[,]
                {
                    { "dashboard", true, "總覽", "/dashboard", 5 },
                    { "repositories", true, "程式庫", "/repositories", 65 }
                });

            migrationBuilder.InsertData(
                schema: "access",
                table: "RoleGroupFeatures",
                columns: new[] { "FeatureId", "GroupId" },
                values: new object[,]
                {
                    { "dashboard", "workspace" },
                    { "repositories", "workspace" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModelCharges_ConversationId",
                schema: "inference",
                table: "ModelCharges",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelCharges_CreatedAt",
                schema: "inference",
                table: "ModelCharges",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ModelCharges_OwnerId_CreatedAt",
                schema: "inference",
                table: "ModelCharges",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelCharges_PriceId",
                schema: "inference",
                table: "ModelCharges",
                column: "PriceId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelPrices_CreatedBy",
                schema: "inference",
                table: "ModelPrices",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ModelPrices_Provider_ModelId_EffectiveAt",
                schema: "inference",
                table: "ModelPrices",
                columns: new[] { "Provider", "ModelId", "EffectiveAt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryImports_DocumentId",
                schema: "knowledge",
                table: "RepositoryImports",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryImports_OwnerId_CollectionId_Repository_Commit_Path",
                schema: "knowledge",
                table: "RepositoryImports",
                columns: new[] { "OwnerId", "CollectionId", "Repository", "Commit", "Path" });

            migrationBuilder.CreateIndex(
                name: "IX_WebSearches_OwnerId_CreatedAt",
                schema: "inference",
                table: "WebSearches",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WebSearches_OwnerId_IdempotencyKey",
                schema: "inference",
                table: "WebSearches",
                columns: new[] { "OwnerId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebSearches_RunId",
                schema: "inference",
                table: "WebSearches",
                column: "RunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
                migrationBuilder.Sql("IF COL_LENGTH('knowledge.Chunks','EmbeddingVector1024') IS NOT NULL EXEC(N'ALTER TABLE [knowledge].[Chunks] DROP COLUMN [EmbeddingVector1024]');");
            migrationBuilder.DropTable(
                name: "ModelCharges",
                schema: "inference");

            migrationBuilder.DropTable(
                name: "RepositoryConnections",
                schema: "workspace");

            migrationBuilder.DropTable(
                name: "RepositoryImports",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "WebSearches",
                schema: "inference");

            migrationBuilder.DropTable(
                name: "ModelPrices",
                schema: "inference");

            migrationBuilder.DeleteData(
                schema: "access",
                table: "RoleGroupFeatures",
                keyColumns: new[] { "FeatureId", "GroupId" },
                keyValues: new object[] { "dashboard", "workspace" });

            migrationBuilder.DeleteData(
                schema: "access",
                table: "RoleGroupFeatures",
                keyColumns: new[] { "FeatureId", "GroupId" },
                keyValues: new object[] { "repositories", "workspace" });

            migrationBuilder.DeleteData(
                schema: "access",
                table: "Features",
                keyColumn: "Id",
                keyValue: "dashboard");

            migrationBuilder.DeleteData(
                schema: "access",
                table: "Features",
                keyColumn: "Id",
                keyValue: "repositories");
        }
    }
}
