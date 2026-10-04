using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AiNexus.BuildingBlocks.Migrations
{
    /// <inheritdoc />
    public partial class KnowledgeAndJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "knowledge");

            migrationBuilder.EnsureSchema(
                name: "collaboration");

            migrationBuilder.CreateTable(
                name: "ModelInvocations",
                schema: "inference",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ModelId = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    InputTokens = table.Column<long>(type: "bigint", nullable: true),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelInvocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelInvocations_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Resources",
                schema: "collaboration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Resources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Resources_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BackgroundJobs",
                schema: "operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Stage = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ActiveKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LeaseToken = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeaseUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancelRequested = table.Column<bool>(type: "bit", nullable: false),
                    Attempt = table.Column<int>(type: "int", nullable: false),
                    CompletedUnits = table.Column<int>(type: "int", nullable: false),
                    TotalUnits = table.Column<int>(type: "int", nullable: true),
                    ErrorCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackgroundJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BackgroundJobs_Resources_ResourceId",
                        column: x => x.ResourceId,
                        principalSchema: "collaboration",
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BackgroundJobs_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Collections",
                schema: "knowledge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Collections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Collections_Resources_Id",
                        column: x => x.Id,
                        principalSchema: "collaboration",
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResourceAttachments",
                schema: "attachments",
                columns: table => new
                {
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttachmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceAttachments", x => new { x.ResourceId, x.AttachmentId });
                    table.ForeignKey(
                        name: "FK_ResourceAttachments_Attachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalSchema: "attachments",
                        principalTable: "Attachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResourceAttachments_Resources_ResourceId",
                        column: x => x.ResourceId,
                        principalSchema: "collaboration",
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResourceGroups",
                schema: "collaboration",
                columns: table => new
                {
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceGroups", x => new { x.ResourceId, x.GroupId });
                    table.ForeignKey(
                        name: "FK_ResourceGroups_Resources_ResourceId",
                        column: x => x.ResourceId,
                        principalSchema: "collaboration",
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ResourceGroups_RoleGroups_GroupId",
                        column: x => x.GroupId,
                        principalSchema: "access",
                        principalTable: "RoleGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResourceMembers",
                schema: "collaboration",
                columns: table => new
                {
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceMembers", x => new { x.ResourceId, x.UserId });
                    table.ForeignKey(
                        name: "FK_ResourceMembers_Resources_ResourceId",
                        column: x => x.ResourceId,
                        principalSchema: "collaboration",
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ResourceMembers_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConversationCollections",
                schema: "knowledge",
                columns: table => new
                {
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CollectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationCollections", x => new { x.ConversationId, x.CollectionId });
                    table.ForeignKey(
                        name: "FK_ConversationCollections_Collections_CollectionId",
                        column: x => x.CollectionId,
                        principalSchema: "knowledge",
                        principalTable: "Collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationCollections_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "conversations",
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                schema: "knowledge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CollectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AttachmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    PageCount = table.Column<int>(type: "int", nullable: false),
                    ChunkCount = table.Column<int>(type: "int", nullable: false),
                    EmbeddingProfile = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Warning = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documents_Attachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalSchema: "attachments",
                        principalTable: "Attachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Documents_Collections_CollectionId",
                        column: x => x.CollectionId,
                        principalSchema: "knowledge",
                        principalTable: "Collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Documents_Resources_Id",
                        column: x => x.Id,
                        principalSchema: "collaboration",
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Chunks",
                schema: "knowledge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PageNumber = table.Column<int>(type: "int", nullable: false),
                    Ordinal = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    EmbeddingJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmbeddingProfile = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Chunks_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "knowledge",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentPages",
                schema: "knowledge",
                columns: table => new
                {
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PageNumber = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Extraction = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    NeedsReview = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentPages", x => new { x.DocumentId, x.PageNumber });
                    table.ForeignKey(
                        name: "FK_DocumentPages_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "knowledge",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MessageCitations",
                schema: "knowledge",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PageNumber = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    Excerpt = table.Column<string>(type: "nvarchar(800)", maxLength: 800, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageCitations", x => new { x.MessageId, x.Number });
                    table.ForeignKey(
                        name: "FK_MessageCitations_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "knowledge",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MessageCitations_Messages_MessageId",
                        column: x => x.MessageId,
                        principalSchema: "conversations",
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "access",
                table: "Features",
                columns: new[] { "Id", "Enabled", "Name", "Route", "SortOrder" },
                values: new object[,]
                {
                    { "knowledge", true, "知識庫", "/knowledge", 30 },
                    { "tasks", true, "背景任務", "/tasks", 70 }
                });

            migrationBuilder.InsertData(
                schema: "access",
                table: "RoleGroupFeatures",
                columns: new[] { "FeatureId", "GroupId" },
                values: new object[,]
                {
                    { "knowledge", "workspace" },
                    { "tasks", "workspace" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobs_ActiveKey",
                schema: "operations",
                table: "BackgroundJobs",
                column: "ActiveKey",
                unique: true,
                filter: "[ActiveKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobs_OwnerId_CreatedAt",
                schema: "operations",
                table: "BackgroundJobs",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobs_ResourceId",
                schema: "operations",
                table: "BackgroundJobs",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobs_Status_LeaseUntil_CreatedAt",
                schema: "operations",
                table: "BackgroundJobs",
                columns: new[] { "Status", "LeaseUntil", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Chunks_DocumentId_Ordinal",
                schema: "knowledge",
                table: "Chunks",
                columns: new[] { "DocumentId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversationCollections_CollectionId",
                schema: "knowledge",
                table: "ConversationCollections",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_AttachmentId",
                schema: "knowledge",
                table: "Documents",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_CollectionId_Status",
                schema: "knowledge",
                table: "Documents",
                columns: new[] { "CollectionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageCitations_DocumentId",
                schema: "knowledge",
                table: "MessageCitations",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelInvocations_OwnerId_CreatedAt",
                schema: "inference",
                table: "ModelInvocations",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ResourceAttachments_AttachmentId",
                schema: "attachments",
                table: "ResourceAttachments",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceGroups_GroupId",
                schema: "collaboration",
                table: "ResourceGroups",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceMembers_UserId",
                schema: "collaboration",
                table: "ResourceMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Resources_OwnerId_Kind_UpdatedAt",
                schema: "collaboration",
                table: "Resources",
                columns: new[] { "OwnerId", "Kind", "UpdatedAt" });

            // Portable JSON is the canonical EF representation. SQL 2025 gets an optional
            // native accelerator; dynamic SQL keeps this migration valid on older servers.
            if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
                migrationBuilder.Sql("IF CONVERT(int,SERVERPROPERTY('ProductMajorVersion')) >= 17 EXEC(N'ALTER TABLE [knowledge].[Chunks] ADD [EmbeddingVector] VECTOR(768) NULL');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BackgroundJobs",
                schema: "operations");

            migrationBuilder.DropTable(
                name: "Chunks",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "ConversationCollections",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "DocumentPages",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "MessageCitations",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "ModelInvocations",
                schema: "inference");

            migrationBuilder.DropTable(
                name: "ResourceAttachments",
                schema: "attachments");

            migrationBuilder.DropTable(
                name: "ResourceGroups",
                schema: "collaboration");

            migrationBuilder.DropTable(
                name: "ResourceMembers",
                schema: "collaboration");

            migrationBuilder.DropTable(
                name: "Documents",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "Collections",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "Resources",
                schema: "collaboration");

            migrationBuilder.DeleteData(
                schema: "access",
                table: "RoleGroupFeatures",
                keyColumns: new[] { "FeatureId", "GroupId" },
                keyValues: new object[] { "knowledge", "workspace" });

            migrationBuilder.DeleteData(
                schema: "access",
                table: "RoleGroupFeatures",
                keyColumns: new[] { "FeatureId", "GroupId" },
                keyValues: new object[] { "tasks", "workspace" });

            migrationBuilder.DeleteData(
                schema: "access",
                table: "Features",
                keyColumn: "Id",
                keyValue: "knowledge");

            migrationBuilder.DeleteData(
                schema: "access",
                table: "Features",
                keyColumn: "Id",
                keyValue: "tasks");
        }
    }
}
