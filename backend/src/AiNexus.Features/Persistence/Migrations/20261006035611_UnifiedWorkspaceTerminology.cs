using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiNexus.Features.Persistence.Migrations;

public partial class UnifiedWorkspaceTerminology : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Upgrade built-in labels only; preserve administrator-defined names and grants.
        Rename(migrationBuilder, "admin", "管理", "平台管理");
        Rename(migrationBuilder, "artifacts", "成果", "成果文件");
        Rename(migrationBuilder, "integrations", "來源", "資料來源");
        Rename(migrationBuilder, "knowledge", "知識", "知識庫");
        Rename(migrationBuilder, "quality", "評測", "品質評測");
        Rename(migrationBuilder, "tasks", "任務", "背景任務");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        Rename(migrationBuilder, "admin", "平台管理", "管理");
        Rename(migrationBuilder, "artifacts", "成果文件", "成果");
        Rename(migrationBuilder, "integrations", "資料來源", "來源");
        Rename(migrationBuilder, "knowledge", "知識庫", "知識");
        Rename(migrationBuilder, "quality", "品質評測", "評測");
        Rename(migrationBuilder, "tasks", "背景任務", "任務");
    }

    private static void Rename(MigrationBuilder migrationBuilder, string id, string previous, string current) =>
        migrationBuilder.Sql($"UPDATE [access].[Features] SET [Name] = N'{current}' WHERE [Id] = N'{id}' AND [Name] = N'{previous}';");
}
