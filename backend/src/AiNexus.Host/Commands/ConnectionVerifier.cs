using AiNexus.Data.App;
using AiNexus.Data.App.AccessControl;
using AiNexus.Data.App.Audit;
using System.Text;
using System.Text.Json;
using AiNexus.Features.Admin.AccessControl;
using AiNexus.Features.Work.Files.Extraction;
using AiNexus.Features.Admin.Identity.Authentication;
using AiNexus.Features.Platform.Inference;
using AiNexus.Features.Platform.Inference.Models;
using AiNexus.Features.Platform.Inference.Providers;
using AiNexus.Features.Work.Knowledge.Retrieval;
using AiNexus.Platform.Data.Sql;
using AiNexus.Platform.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Host.Commands;

/// <summary>
/// 跨子系統連線整合實測器。
/// 使用合成資料實測 SQL 讀寫交易、Active Directory 服務帳號綁定、模型即時串流、附件抽取辨識與檢索向量模型。
/// 會真的呼叫已設定的模型，因此會消耗少量測試配額。
/// </summary>
public static class ConnectionVerifier
{
    /// <summary>
    /// 執行完整連線檢查並將結果寫入報告檔案。
    /// </summary>
    public static async Task<bool> VerifyAsync(IServiceProvider root, string reportPath, CancellationToken ct)
    {
        using var scope = root.CreateScope();
        var services = scope.ServiceProvider;
        var results = new List<object>();
        var passed = true;

        async Task Check(string name, Func<Task<string>> action)
        {
            try
            {
                var detail = await action();
                results.Add(new { name, passed = true, detail });
                Console.WriteLine($"{name}: PASS · {detail}");
            }
            catch (Exception ex)
            {
                passed = false;
                var detail = ex is ExternalServiceException || name == "SQL"
                    ? HostCommand.Describe(ex)
                    : $"連線未通過（{ex.GetType().Name}）。";
                results.Add(new { name, passed = false, detail });
                Console.WriteLine($"{name}: FAIL · {detail}");
            }
        }

        await Check("SQL", async () =>
        {
            await services.GetRequiredService<ISqlDatabase<AppDbContext>>().QuerySingleAsync<int>("SELECT 1", commandTimeout: 5, cancellationToken: ct);
            await VerifyPersistenceAsync(services, ct);
            var db = services.GetRequiredService<AppDbContext>();

            if (!await db.Set<RoleGroupRole>().AnyAsync(x => x.RoleId == BuiltInAccess.MemberRole && x.GroupId == BuiltInAccess.WorkspaceGroup, ct)
                || !await db.Set<RoleGroupFeature>().AnyAsync(x => x.GroupId == BuiltInAccess.WorkspaceGroup && x.FeatureId == FeatureCatalog.Chat, ct))
            {
                throw new ExternalServiceException(Error.Unavailable("default_access_missing"), "預設角色、群組與 chat 功能關聯尚未完成。");
            }

            var members = await db.Set<UserRole>().CountAsync(x => x.RoleId == BuiltInAccess.MemberRole, ct);
            return $"SQL EF Core／Dapper CRUD 與清理通過；預設 chat 授權完整，member 對應 {members} 位使用者";
        });

        await Check("AD", async () =>
        {
            await services.GetRequiredService<IAdAuthenticator>().VerifyServiceAsync(ct);
            return "服務帳號加密 LDAP bind 成功；個人登入仍需實際帳號驗收";
        });
        await Check("Inference", async () =>
        {
            var router = services.GetRequiredService<InferenceRouter>();
            var models = services.GetRequiredService<IOptions<InferenceOptions>>().Value.Models;
            var defaultId = services.GetRequiredService<ModelPresentation>().DefaultId;
            var profile = models.FirstOrDefault(x => x.Id == defaultId)
                ?? throw new ExternalServiceException(Error.Unavailable("model_not_configured"), "請設定預設模型。");
            var provider = router.For(profile.Provider);
            if (!(await provider.InstalledModelsAsync(ct)).Contains(profile.NativeId))
            {
                throw new ExternalServiceException(Error.Unavailable("model_unavailable"), "目前的模型服務無法使用系統預設模型。");
            }

            var efforts = profile.ReasoningEfforts.Count > 0 ? profile.ReasoningEfforts : ["auto"];
            var counts = new List<string>();
            foreach (var effort in efforts)
            {
                var (text, done) = await StreamAsync(
                    provider,
                    profile.NativeId,
                    new("user", "2+2 等於多少？請只回覆一個數字，不要解釋。"),
                    new(profile.ContextTokens, Math.Min(profile.MaxOutputTokens, 512), .2, "簡短回答。", effort, profile.ReasoningControl),
                    ct);

                if (!done || text.Length == 0)
                {
                    throw new InvalidDataException("模型未傳回內容或傳輸未完成。");
                }
                counts.Add($"{effort} 收到 {text.Length} 字元");
            }
            return $"{profile.Provider} 系統預設模型真實串流完成：{string.Join("；", counts)}";
        });
        await Check("AttachmentInference", async () =>
        {
            var profiles = await services.GetRequiredService<ModelCatalog>().ProfilesAsync(ct);
            var defaultId = services.GetRequiredService<ModelPresentation>().DefaultId;
            var profile = profiles.FirstOrDefault(x => x.Id == defaultId)
                ?? throw new ExternalServiceException(Error.Unavailable("model_not_configured"), "請設定預設模型。");
            // 只送合成內容，不含任何個人檔案、對話紀錄或身分憑證。
            var document = services.GetRequiredService<DocumentExtractor>().Extract("connection-check.txt", Encoding.UTF8.GetBytes("Project verification code: PROBECHECK42"), ct).Value.Text;
            var images = profile.SupportsImages ? new[] { new InferenceImage(Guid.NewGuid(), "image/png", Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAIAAAACACAIAAABMXPacAAABWklEQVR4nO3OQQ0AMBAEofVv+iqDxzRBALvtg/wgzg/i/CDOD+L8IM4P4vwgzg/i/CDOD+L8IM4P4vwgzg/i/CDOD+L8IM4P4vwgzg/i/CDOD+L8IM4P4vwgzg/i/CDOD+L8IM4P4vwgzg/i/CDOD+L8IM4P4vwgzg/i/CDOD+L8IM4P4vwgzg/i/CDOD+L8IM4P4vwgzg/i/CDOD+L8IM4P4vwgzg/i/CDOD+L8IM4P4vwgzg/i/CDOD+L8IM4P4vwgzg/i/CDOD+L8IM4P4vwgzg/i/CDOD+L8IM4P4vwgzg/i/CDOD+L8IM4P4vwgzg/i/CDOD+L8IM4P4vwgzg/i/CDOD+L8IM4P4vwgzg/i/CDOD+L8IM4P4vwgzg/i/CDOD+L8IM4P4vwgzg/i/CDOD+L8IM4P4vwgzg/i/CDOD+L8IM4P4vwgzg/i/CDOD+L8IM4P4vzg2h4gaMOyHY2XLAAAAABJRU5ErkJggg=="), 4096) } : [];
            var prompt = "Read the attached document and reply with its project verification code." + (images.Length > 0 ? " Also identify the dominant color in the attached image using one English color word." : "") + "\n<document>\n" + document + "\n</document>";
            var parameters = new GenerationParameters(profile.ContextTokens, Math.Min(profile.MaxOutputTokens, 512), .2, "Answer concisely in English.", profile.DefaultReasoningEffort, profile.ReasoningControl, profile.SupportsImages);
            var (answer, done) = await StreamAsync(services.GetRequiredService<InferenceRouter>().For(profile.Provider), profile.NativeId, new("user", prompt, images), parameters, ct);
            if (!done || !answer.Contains("PROBECHECK42", StringComparison.OrdinalIgnoreCase) || (images.Length > 0 && !answer.Contains("red", StringComparison.OrdinalIgnoreCase)))
                throw new ExternalServiceException(Error.Unavailable("attachment_probe_failed"), "模型未正確識別合成文件或圖片，請檢查模型能力設定。");
            return images.Length > 0 ? "真實模型已辨識合成文件代碼及紅色 PNG 圖片；未傳送私人資料" : "真實模型已辨識合成文件代碼；此 profile 未啟用圖片能力";
        });
        await Check("RetrievalModels", async () => {
            var result = await services.GetRequiredService<RetrievalModelProbe>().CheckAsync(null, ct);
            if (result.Embedding.Available != true || result.Rerank.Available != true) throw new ExternalServiceException(Error.Unavailable("retrieval_models_unavailable"), result.Embedding.Notice + " " + result.Rerank.Notice);
            return result.Embedding.Notice + " " + result.Rerank.Notice;
        });
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(new { checkedAt = services.GetRequiredService<TimeProvider>().GetUtcNow(), results }, HostCommand.ReportJson), ct);
        return passed;
    }

    /// <summary>送出單一訊息並收齊串流；每次呼叫各自限時 60 秒，避免模型卡住讓整個指令停在原地。</summary>
    private static async Task<(string Text, bool Done)> StreamAsync(IInferenceProvider provider, string model, InferenceMessage message, GenerationParameters parameters, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        var text = new StringBuilder();
        var done = false;
        await foreach (var chunk in provider.StreamAsync(model, [message], parameters, timeout.Token))
        {
            text.Append(chunk.Text);
            done |= chunk.Done;
        }
        return (text.ToString(), done);
    }

    private static async Task VerifyPersistenceAsync(IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var sql = services.GetRequiredService<ISqlDatabase<AppDbContext>>();
        var probe = new AuditEvent { OwnerId = Guid.Empty, ResourceId = Guid.NewGuid(), Action = "connection_check", Result = "ef_insert" };
        var removed = true;
        try
        {
            db.AuditEvents.Add(probe);
            await db.SaveChangesAsync(ct);
            var row = new { probe.Id, probe.ResourceId };
            if (await sql.QuerySingleAsync<string>("SELECT [Result] FROM [audit].[AuditEvents] WHERE [Id] = @Id AND [ResourceId] = @ResourceId", row, commandTimeout: 5, cancellationToken: ct) != "ef_insert")
                throw new InvalidDataException("Committed EF write could not be read through Dapper.");
            var changed = await sql.ExecuteAsync("UPDATE [audit].[AuditEvents] SET [Result] = @Result WHERE [Id] = @Id AND [ResourceId] = @ResourceId", new { probe.Id, probe.ResourceId, Result = "dapper_update" }, commandTimeout: 5, cancellationToken: ct);
            var result = await db.AuditEvents.AsNoTracking().Where(x => x.Id == probe.Id).Select(x => x.Result).SingleAsync(ct);
            if (changed != 1 || result != "dapper_update") throw new InvalidDataException("Dapper update could not be read by EF.");
        }
        finally
        {
            if (probe.Id != 0)
            {
                // 探針沒有使用者身分，且只會刪除自己剛建立的隨機資料列。
                await sql.ExecuteAsync("DELETE FROM [audit].[AuditEvents] WHERE [Id] = @Id AND [ResourceId] = @ResourceId AND [Action] = @Action", new { probe.Id, probe.ResourceId, probe.Action }, commandTimeout: 5, cancellationToken: CancellationToken.None);
                removed = await sql.QuerySingleAsync<int>("SELECT COUNT(*) FROM [audit].[AuditEvents] WHERE [Id] = @Id AND [ResourceId] = @ResourceId", new { probe.Id, probe.ResourceId }, commandTimeout: 5, cancellationToken: CancellationToken.None) == 0;
            }
        }
        // 探針紀錄若清不掉，必須視為失敗，否則測試資料會殘留在稽核表。
        // 注意：finally 中的清理若拋出例外，會蓋掉先前的測試失敗例外。
        if (!removed) throw new InvalidDataException("連線測試之暫時性測試紀錄清理未順利完成。");
    }
}
