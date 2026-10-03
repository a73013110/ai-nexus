using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiNexus.BuildingBlocks.Migrations
{
    /// <inheritdoc />
    public partial class WorkspaceExtensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "attachments");

            migrationBuilder.EnsureSchema(
                name: "library");

            migrationBuilder.AddColumn<string>(
                name: "ErrorCode",
                schema: "conversations",
                table: "Messages",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                schema: "conversations",
                table: "Conversations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsFavorite",
                schema: "conversations",
                table: "Conversations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SystemInstruction",
                schema: "conversations",
                table: "Conversations",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Attachments",
                schema: "attachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    Data = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    ExtractedText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Attachments_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConversationLabels",
                schema: "conversations",
                columns: table => new
                {
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationLabels", x => new { x.ConversationId, x.Name });
                    table.ForeignKey(
                        name: "FK_ConversationLabels_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "conversations",
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PromptTemplates",
                schema: "library",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", maxLength: 12000, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromptTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PromptTemplates_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MessageAttachments",
                schema: "attachments",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttachmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageAttachments", x => new { x.MessageId, x.AttachmentId });
                    table.ForeignKey(
                        name: "FK_MessageAttachments_Attachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalSchema: "attachments",
                        principalTable: "Attachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MessageAttachments_Messages_MessageId",
                        column: x => x.MessageId,
                        principalSchema: "conversations",
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_OwnerId_IsDeleted_IsArchived_IsFavorite_UpdatedAt",
                schema: "conversations",
                table: "Conversations",
                columns: new[] { "OwnerId", "IsDeleted", "IsArchived", "IsFavorite", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_OwnerId_CreatedAt",
                schema: "attachments",
                table: "Attachments",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageAttachments_AttachmentId",
                schema: "attachments",
                table: "MessageAttachments",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PromptTemplates_OwnerId_UpdatedAt",
                schema: "library",
                table: "PromptTemplates",
                columns: new[] { "OwnerId", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationLabels",
                schema: "conversations");

            migrationBuilder.DropTable(
                name: "MessageAttachments",
                schema: "attachments");

            migrationBuilder.DropTable(
                name: "PromptTemplates",
                schema: "library");

            migrationBuilder.DropTable(
                name: "Attachments",
                schema: "attachments");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_OwnerId_IsDeleted_IsArchived_IsFavorite_UpdatedAt",
                schema: "conversations",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "ErrorCode",
                schema: "conversations",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                schema: "conversations",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "IsFavorite",
                schema: "conversations",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "SystemInstruction",
                schema: "conversations",
                table: "Conversations");
        }
    }
}
