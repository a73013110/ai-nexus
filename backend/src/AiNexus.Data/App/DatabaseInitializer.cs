using AiNexus.Platform.Data;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Data.Sql;
using Microsoft.Extensions.Options;

namespace AiNexus.Data.App;

/// <summary>
/// 在 SQL Server 上建立 <c>AiNexus</c> 專用的空資料庫並開啟 RCSI；資料表等結構由 <c>database/App</c> 的 dacpac 部署。
/// </summary>
public sealed class DatabaseInitializer(
    IOptions<DatabasesOptions> databases,
    ISqlDatabase<MasterDatabase> master,
    DatabaseSchema schema)
{
    /// <summary>
    /// 資料庫不存在時建立它，並回報 RCSI 狀態；不建立或修改任何結構。
    /// </summary>
    /// <returns>若 READ_COMMITTED_SNAPSHOT 已開啟回傳 <see langword="true"/>；否則回傳 <see langword="false"/>。</returns>
    public async Task<bool> InitializeAsync(CancellationToken ct)
    {
        var nexus = databases.Value.For(AppDbContext.DatabaseName);
        if (!nexus.Configured)
        {
            throw new ExternalServiceException(
                Error.Unavailable("storage_not_configured"),
                "請先設定 Databases.Nexus 的 Server、Name，以及秘密檔的 User／Password（或 IntegratedSecurity=true）。");
        }

        var target = nexus.Name;
        // 只允許操作 AiNexus 專用資料庫，避免設定誤指到既有的其他系統或業務資料庫。
        if (target != "AiNexus")
        {
            throw new ExternalServiceException(
                Error.Invalid("database_name"),
                "本初始化指令只允許建立或更新 AiNexus 專用資料庫。");
        }

        var exists = await master.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM sys.databases WHERE name = @Name",
            new { Name = target },
            commandTimeout: 5,
            cancellationToken: ct);

        // 只有全新建立的資料庫才會在建立當下切換 RCSI（見 CreateAsync）；既有資料庫不在此處理。
        if (exists == 0)
        {
            await CreateAsync(master, target, ct);
        }

        return await schema.ReadCommittedSnapshotAsync(ct) ?? false;
    }

    /// <summary>
    /// 建立全新資料庫，並立即啟用 READ_COMMITTED_SNAPSHOT (RCSI)。
    /// </summary>
    /// <remarks>
    /// 為什麼在這裡執行：
    /// <c>ALTER DATABASE ... SET READ_COMMITTED_SNAPSHOT ON</c> 不能在事務中執行，且需要排他性連線（Exclusive Access）。
    /// 全新剛建立的資料庫沒有任何外部連線，是唯一能保證 100% 成功無衝突切換的時間點。
    /// 若為已運行的生產資料庫，絕不可自動執行，需由 DBA 在維護窗口操作。
    /// </remarks>
    public static async Task CreateAsync(ISqlDatabase<MasterDatabase> master, string name, CancellationToken ct)
    {
        var quoted = "[" + name.Replace("]", "]]", StringComparison.Ordinal) + "]";
        await master.ExecuteAsync("CREATE DATABASE " + quoted, commandTimeout: 30, cancellationToken: ct);
        await master.ExecuteAsync("ALTER DATABASE " + quoted + " SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE", commandTimeout: 30, cancellationToken: ct);
    }
}
