using System.Text;
using System.Text.Json;
using AiNexus.Platform.Data;
using AiNexus.Features.Persistence;
using AiNexus.Features.Identity;
using AiNexus.Features.Inference;
using AiNexus.Features.AccessControl;
using AiNexus.Features.Attachments;
using EDoc.Core.Database.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AiNexus.Platform.Errors;

namespace AiNexus.Api.Commands;

public static class ConnectionVerifier
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public static async Task<bool> VerifyAsync(IServiceProvider services, IConfiguration configuration, string contentRoot, CancellationToken ct)
    {
        var results = new List<object>();
        var passed = true;
        async Task Check(string name, Func<Task<string>> action)
        {
            try { var detail = await action(); results.Add(new { name, passed = true, detail }); Console.WriteLine($"{name}: PASS · {detail}"); }
            catch (Exception ex) { passed = false; var detail = ex is ApiException api ? api.Message : name == "SQL" ? LocalDatabaseSettings.Diagnose(ex) : $"連線未通過（{ex.GetType().Name}）。"; results.Add(new { name, passed = false, detail }); Console.WriteLine($"{name}: FAIL · {detail}"); }
        }
        await Check("SQL", async () =>
        {
            await services.GetRequiredService<IDbHelper<INexusDatabase>>().QuerySingleAsync<int>("SELECT 1", commandTimeout: 5, cancellationToken: ct);
            await services.GetRequiredService<DatabaseSchema>().RequireCurrentAsync(ct);
            await VerifyPersistenceAsync(services, ct);
            var db = services.GetRequiredService<NexusDbContext>();
            if (!await db.Set<RoleGroupRole>().AnyAsync(x => x.RoleId == BuiltInAccess.MemberRole && x.GroupId == BuiltInAccess.WorkspaceGroup, ct)
                || !await db.Set<RoleGroupFeature>().AnyAsync(x => x.GroupId == BuiltInAccess.WorkspaceGroup && x.FeatureId == BuiltInAccess.ChatFeature, ct))
                throw new ApiException(503, "default_access_missing", "預設角色、群組與 chat 功能關聯尚未完成。");
            var members = await db.Set<UserRole>().CountAsync(x => x.RoleId == BuiltInAccess.MemberRole, ct);
            return $"SQL migrations、EF Core／DbHelper CRUD 與清理通過；預設 chat 授權完整，member 對應 {members} 位使用者";
        });
        await Check("AD", async () => { await services.GetRequiredService<IAdAuthenticator>().VerifyServiceAsync(ct); return "服務帳號加密 LDAP bind 成功；個人登入仍需實際帳號驗收"; });
        await Check("Inference", async () =>
        {
            var router = services.GetRequiredService<InferenceRouter>();
            var options = services.GetRequiredService<IOptions<InferenceOptions>>().Value;
            var profile = options.Models.FirstOrDefault(x => x.Id == (options.DefaultModelId ?? options.Models.FirstOrDefault()?.Id))
                ?? throw new ApiException(503, "model_not_configured", "請設定預設模型。");
            var provider = router.For(profile.Provider);
            var models = await provider.InstalledModelsAsync(ct);
            if (!models.Contains(profile.NativeId)) throw new ApiException(503, "model_unavailable", "目前的模型服務無法使用系統預設模型。");
            var efforts = profile.ReasoningEfforts.Count > 0 ? profile.ReasoningEfforts : ["auto"];
            var counts = new List<string>();
            foreach (var effort in efforts)
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(60));
                var text = new StringBuilder(); var done = false;
                await foreach (var chunk in provider.StreamAsync(profile.NativeId, [new("user", "2+2 等於多少？請只回覆一個數字，不要解釋。")], new(profile.ContextTokens, Math.Min(profile.MaxOutputTokens, 512), .2, "簡短回答。", effort, profile.ReasoningControl), timeout.Token)) { text.Append(chunk.Text); done |= chunk.Done; }
                if (!done || text.Length == 0) throw new InvalidDataException("Empty or incomplete model response.");
                counts.Add($"{effort} 收到 {text.Length} 字元");
            }
            return $"{profile.Provider} 系統預設模型真實串流完成：{string.Join("；", counts)}";
        });
        await Check("AttachmentInference", async () =>
        {
            var options = services.GetRequiredService<IOptions<InferenceOptions>>().Value;
            var profiles = await services.GetRequiredService<ModelCatalog>().ProfilesAsync(ct);
            var profile = profiles.FirstOrDefault(x => x.Id == (options.DefaultModelId ?? options.Models.FirstOrDefault()?.Id))
                ?? throw new ApiException(503, "model_not_configured", "請設定預設模型。");
            // Synthetic content only: no personal files, prompt history or identity credentials.
            var (_, document) = services.GetRequiredService<DocumentExtractor>().Extract("connection-check.txt", Encoding.UTF8.GetBytes("Project verification code: NEXUSCHECK42"), ct);
            var images = profile.SupportsImages ? new[] { new InferenceImage(Guid.NewGuid(), "image/png", Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAIAAAACACAIAAABMXPacAAABWklEQVR4nO3OQQ0AMBAEofVv+iqDxzRBALvtg/wgzg/i/CDOD+L8IM4P4vwgzg/i/CDOD+L8IM4P4vwgzg/i/CDOD+L8IM4P4vwgzg/i/CDOD+L8IM4P4vwgzg/i/CDOD+L8IM4P4vwgzg/i/CDOD+L8IM4P4vwgzg/i/CDOD+L8IM4P4vwgzg/i/CDOD+L8IM4P4vwgzg/i/CDOD+L8IM4P4vwgzg/i/CDOD+L8IM4P4vwgzg/i/CDOD+L8IM4P4vwgzg/i/CDOD+L8IM4P4vwgzg/i/CDOD+L8IM4P4vwgzg/i/CDOD+L8IM4P4vwgzg/i/CDOD+L8IM4P4vwgzg/i/CDOD+L8IM4P4vwgzg/i/CDOD+L8IM4P4vwgzg/i/CDOD+L8IM4P4vwgzg/i/CDOD+L8IM4P4vwgzg/i/CDOD+L8IM4P4vwgzg/i/CDOD+L8IM4P4vzg2h4gaMOyHY2XLAAAAABJRU5ErkJggg=="), 4096) } : [];
            var prompt = "Read the attached document and reply with its project verification code." + (images.Length > 0 ? " Also identify the dominant color in the attached image using one English color word." : "") + "\n<document>\n" + document + "\n</document>";
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(60));
            var text = new StringBuilder(); var done = false;
            var parameters = new GenerationParameters(profile.ContextTokens, Math.Min(profile.MaxOutputTokens, 512), .2, "Answer concisely in English.", profile.DefaultReasoningEffort, profile.ReasoningControl, profile.SupportsImages);
            await foreach (var chunk in services.GetRequiredService<InferenceRouter>().For(profile.Provider).StreamAsync(profile.NativeId, [new("user", prompt, images)], parameters, timeout.Token)) { text.Append(chunk.Text); done |= chunk.Done; }
            if (!done || !text.ToString().Contains("NEXUSCHECK42", StringComparison.OrdinalIgnoreCase) || (images.Length > 0 && !text.ToString().Contains("red", StringComparison.OrdinalIgnoreCase)))
                throw new ApiException(503, "attachment_probe_failed", "模型未正確識別合成文件或圖片，請檢查模型能力設定。");
            return images.Length > 0 ? "真實模型已辨識合成文件代碼及紅色 PNG 圖片；未傳送私人資料" : "真實模型已辨識合成文件代碼；此 profile 未啟用圖片能力";
        });
        await Check("RetrievalModels", async () => {
            var result = await services.GetRequiredService<AiNexus.Features.Knowledge.RetrievalModelProbe>().CheckAsync(null, ct);
            if (result.Embedding.Available != true || result.Rerank.Available != true) throw new ApiException(503, "retrieval_models_unavailable", result.Embedding.Notice + " " + result.Rerank.Notice);
            return result.Embedding.Notice + " " + result.Rerank.Notice;
        });
        var destination = configuration["VerificationOutput"] ?? Path.Combine(contentRoot, "connection-checks.json");
        await File.WriteAllTextAsync(destination, JsonSerializer.Serialize(new { checkedAt = DateTimeOffset.UtcNow, results }, Indented), ct);
        return passed;
    }

    private static async Task VerifyPersistenceAsync(IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<NexusDbContext>();
        var sql = services.GetRequiredService<IDbHelper<INexusDatabase>>();
        var probe = new AuditEvent { OwnerId = Guid.Empty, ResourceId = Guid.NewGuid(), Action = "connection_check", Result = "ef_insert" };
        var removed = true;
        try
        {
            db.AuditEvents.Add(probe);
            await db.SaveChangesAsync(ct);
            var row = new { probe.Id, probe.ResourceId };
            if (await sql.QuerySingleAsync<string>("SELECT [Result] FROM [operations].[AuditEvents] WHERE [Id] = @Id AND [ResourceId] = @ResourceId", row, commandTimeout: 5, cancellationToken: ct) != "ef_insert")
                throw new InvalidDataException("Committed EF write could not be read from a separate Dapper connection.");
            var changed = await sql.ExecuteAsync("UPDATE [operations].[AuditEvents] SET [Result] = @Result WHERE [Id] = @Id AND [ResourceId] = @ResourceId", new { probe.Id, probe.ResourceId, Result = "dapper_update" }, commandTimeout: 5, cancellationToken: ct);
            var result = await db.AuditEvents.AsNoTracking().Where(x => x.Id == probe.Id).Select(x => x.Result).SingleAsync(ct);
            if (changed != 1 || result != "dapper_update") throw new InvalidDataException("Dapper update could not be read by EF.");
        }
        finally
        {
            if (probe.Id != 0)
            {
                // This CLI-only probe has no user identity and touches only its own random resource.
                await sql.ExecuteAsync("DELETE FROM [operations].[AuditEvents] WHERE [Id] = @Id AND [ResourceId] = @ResourceId AND [Action] = @Action", new { probe.Id, probe.ResourceId, probe.Action }, commandTimeout: 5, cancellationToken: CancellationToken.None);
                removed = await sql.QuerySingleAsync<int>("SELECT COUNT(*) FROM [operations].[AuditEvents] WHERE [Id] = @Id AND [ResourceId] = @ResourceId", new { probe.Id, probe.ResourceId }, commandTimeout: 5, cancellationToken: CancellationToken.None) == 0;
            }
        }
        // Reported only after a successful check; a failed check keeps its own exception.
        if (!removed) throw new InvalidDataException("Connection-check cleanup did not complete.");
    }
}
