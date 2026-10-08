using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Persistence;

public sealed class DatabaseSchema(NexusDbContext db)
{
    public async Task<IReadOnlyList<string>> PendingMigrationsAsync(CancellationToken ct)
        => (await db.Database.GetPendingMigrationsAsync(ct)).ToArray();

    public async Task RequireCurrentAsync(CancellationToken ct)
    {
        await RequireCompatibleHistoryAsync(ct);
        if (db.Database.IsSqlServer() && db.Database.HasPendingModelChanges())
            throw new ApiException(503, "model_snapshot_mismatch", "EF 模型與 migration snapshot 不一致，請先建立並提交對應 migration。");
        var pending = await PendingMigrationsAsync(ct);
        if (pending.Count > 0)
            throw new ApiException(503, "migrations_pending",
                $"資料庫尚未套用 migrations：{string.Join(", ", pending)}。請停止舊 host，執行 scripts/Initialize-Database.ps1 或由 DBA 套用 db/migrations.sql，再啟動此版本。");
    }

    public async Task RequireCompatibleHistoryAsync(CancellationToken ct)
    {
        var unknown = (await db.Database.GetAppliedMigrationsAsync(ct)).Except(db.Database.GetMigrations()).ToArray();
        if (unknown.Length > 0)
            throw new ApiException(503, "migration_baseline_mismatch",
                "此資料庫包含其他 migration 基線。本版 InitialCreate 僅適用全新空資料庫；初始化不會刪庫、清空資料或修改歷史，請由管理者處理資料庫版本。");
    }
}
