using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Persistence;

public sealed class DatabaseSchema(NexusDbContext db)
{
    public async Task<IReadOnlyList<string>> PendingMigrationsAsync(CancellationToken ct)
        => (await db.Database.GetPendingMigrationsAsync(ct)).ToArray();

    public async Task RequireCurrentAsync(CancellationToken ct)
    {
        if (db.Database.IsSqlServer() && db.Database.HasPendingModelChanges())
            throw new ApiException(503, "model_snapshot_mismatch", "EF 模型與 migration snapshot 不一致，請先建立並提交對應 migration。");
        var pending = await PendingMigrationsAsync(ct);
        if (pending.Count > 0)
            throw new ApiException(503, "migrations_pending",
                $"資料庫尚未套用 migrations：{string.Join(", ", pending)}。請停止舊 host，執行 scripts/Initialize-Database.ps1 或由 DBA 套用 db/migrations.sql，再啟動此版本。");
    }
}
