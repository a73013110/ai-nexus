using System.Text;
using System.Text.Json;
using AiNexus.Database;
using AiNexus.Modules.Identity;
using AiNexus.Modules.Inference;
using AiNexus.Modules.Operations;
using AiNexus.Modules.AccessControl;
using EDoc.Core.Database.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.BuildingBlocks;

public static class ConnectionVerifier
{
    public static async Task<bool> VerifyAsync(IServiceProvider services, IConfiguration configuration, string contentRoot, CancellationToken ct)
    {
        var results = new List<object>();
        var passed = true;
        async Task Check(string name, Func<Task<string>> action)
        {
            try { var detail = await action(); results.Add(new { name, passed = true, detail }); Console.WriteLine($"{name}: PASS · {detail}"); }
            catch (Exception ex) { passed = false; var detail = ex is ApiException api ? api.Message : name == "SQL" ? LocalDatabaseSettings.Diagnose(ex) : $"連線未通過（{ex.GetType().Name}）。"; results.Add(new { name, passed = false, detail }); Console.WriteLine($"{name}: FAIL · {detail}"); }
        }
        await Check("SQL", async () => {
            await services.GetRequiredService<IDbHelper<INexusDatabase>>().QuerySingleAsync<int>("SELECT 1", commandTimeout: 5, cancellationToken: ct);
            var pending = await services.GetRequiredService<NexusDbContext>().Database.GetPendingMigrationsAsync(ct);
            if (pending.Any()) throw new ApiException(503, "migrations_pending", "SQL 已連線；請先執行 Initialize-Database.ps1 套用 migrations。");
            await VerifyPersistenceAsync(services, ct);
            var db = services.GetRequiredService<NexusDbContext>();
            if (!await db.Set<RoleGroupRole>().AnyAsync(x => x.RoleId == BuiltInAccess.MemberRole && x.GroupId == BuiltInAccess.WorkspaceGroup, ct)
                || !await db.Set<RoleGroupFeature>().AnyAsync(x => x.GroupId == BuiltInAccess.WorkspaceGroup && x.FeatureId == BuiltInAccess.ChatFeature, ct))
                throw new ApiException(503, "default_access_missing", "預設角色、群組與 chat 功能關聯尚未完成。");
            var members = await db.Set<UserRole>().CountAsync(x => x.RoleId == BuiltInAccess.MemberRole, ct);
            return $"SQL migrations、EfHelper／DbHelper CRUD 與清理通過；預設 chat 授權完整，member 對應 {members} 位使用者";
        });
        await Check("AD", async () => { await services.GetRequiredService<IAdAuthenticator>().VerifyServiceAsync(ct); return "服務帳號加密 LDAP bind 成功；個人登入仍需實際帳號驗收"; });
        await Check("Inference", async () => {
            var provider = services.GetRequiredService<IInferenceProvider>();
            var options = services.GetRequiredService<IOptions<InferenceOptions>>().Value;
            var profile = options.Models.FirstOrDefault(x => x.Id == (options.DefaultModelId ?? options.Models.FirstOrDefault()?.Id))
                ?? throw new ApiException(503, "model_not_configured", "請設定預設模型。");
            var models = await provider.InstalledModelsAsync(ct);
            if (!models.Contains(profile.Id)) throw new ApiException(503, "model_unavailable", "目前的模型服務無法使用系統預設模型。");
            var efforts = profile.ReasoningEfforts.Count > 0 ? profile.ReasoningEfforts : ["auto"];
            var counts = new List<string>();
            foreach (var effort in efforts)
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(60));
                var text = new StringBuilder(); var done = false;
                await foreach (var chunk in provider.StreamAsync(profile.Id, [new("user", "2+2 等於多少？請只回覆一個數字，不要解釋。")], new(profile.ContextTokens, Math.Min(profile.MaxOutputTokens, 512), .2, "簡短回答。", effort, profile.ReasoningControl), timeout.Token)) { text.Append(chunk.Text); done |= chunk.Done; }
                if (!done || text.Length == 0) throw new InvalidDataException("Empty or incomplete model response.");
                counts.Add($"{effort} 收到 {text.Length} 字元");
            }
            return $"{options.Provider} 系統預設模型真實串流完成：{string.Join("；", counts)}";
        });
        var destination = configuration["VerificationOutput"] ?? Path.Combine(contentRoot, "connection-checks.json");
        await File.WriteAllTextAsync(destination, JsonSerializer.Serialize(new { checkedAt = DateTimeOffset.UtcNow, results }, new JsonSerializerOptions { WriteIndented = true }), ct);
        return passed;
    }

    private static async Task VerifyPersistenceAsync(IServiceProvider services, CancellationToken ct)
    {
        var ef = services.GetRequiredService<IEfHelper<INexusDatabase>>();
        var sql = services.GetRequiredService<IDbHelper<INexusDatabase>>();
        var probe = new AuditEvent { OwnerId = Guid.Empty, ResourceId = Guid.NewGuid(), Action = "connection_check", Result = "ef_insert" };
        try
        {
            ef.Set<AuditEvent>().Add(probe);
            await ef.SaveChangesAsync(ct);
            var row = new { probe.Id, probe.ResourceId };
            if (await sql.QuerySingleAsync<string>("SELECT [Result] FROM [operations].[AuditEvents] WHERE [Id] = @Id AND [ResourceId] = @ResourceId", row, commandTimeout: 5, cancellationToken: ct) != "ef_insert")
                throw new InvalidDataException("Committed EF write could not be read from a separate Dapper connection.");
            var changed = await sql.ExecuteAsync("UPDATE [operations].[AuditEvents] SET [Result] = @Result WHERE [Id] = @Id AND [ResourceId] = @ResourceId", new { probe.Id, probe.ResourceId, Result = "dapper_update" }, commandTimeout: 5, cancellationToken: ct);
            var result = await ef.Set<AuditEvent>().AsNoTracking().Where(x => x.Id == probe.Id).Select(x => x.Result).SingleAsync(ct);
            if (changed != 1 || result != "dapper_update") throw new InvalidDataException("Dapper update could not be read by EF.");
        }
        finally
        {
            if (probe.Id != 0)
            {
                // This CLI-only probe has no user identity and touches only its own random resource.
                await sql.ExecuteAsync("DELETE FROM [operations].[AuditEvents] WHERE [Id] = @Id AND [ResourceId] = @ResourceId AND [Action] = @Action", new { probe.Id, probe.ResourceId, probe.Action }, commandTimeout: 5, cancellationToken: CancellationToken.None);
                if (await sql.QuerySingleAsync<int>("SELECT COUNT(*) FROM [operations].[AuditEvents] WHERE [Id] = @Id AND [ResourceId] = @ResourceId", new { probe.Id, probe.ResourceId }, commandTimeout: 5, cancellationToken: CancellationToken.None) != 0)
                    throw new InvalidDataException("Connection-check cleanup did not complete.");
            }
        }
    }
}
