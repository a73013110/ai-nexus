using AiNexus.Data.App;
using AiNexus.Data.App.Knowledge;
using AiNexus.Features.Knowledge.Embeddings;
using AiNexus.Platform.Monitoring;
using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Knowledge;

/// <summary>
/// 文件索引的執行期監控，涵蓋知識庫、專案與私人讀取文件。只統計未完成與失敗的文件（靠狀態索引），已就緒的不計入；只要有任一文件失敗即判為降級。
/// </summary>
internal sealed class KnowledgeMonitoring(AppDbContext db, EmbeddingService embedding) : IMonitoringSource
{
    public string Id => "knowledge-index";
    public string Name => "知識索引";
    public int Order => 50;
    public TimeSpan Refresh => TimeSpan.FromSeconds(30);

    public async Task<SubsystemReading> ReadAsync(CancellationToken ct)
    {
        // Document 沒有軟刪除的全域過濾，已刪除的文件必須在這裡明確排除。
        var counts = await db.Set<Document>().AsNoTracking().Where(x => !x.IsDeleted && x.Status != "ready")
            .GroupBy(x => x.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(ct);
        var pending = counts.Where(x => x.Status == "queued").Sum(x => x.Count);
        var failed = counts.Where(x => x.Status == "failed").Sum(x => x.Count);
        var indexing = counts.Sum(x => x.Count) - pending - failed;
        var (status, summary) =
            failed > 0 ? (SubsystemStates.Degraded, $"{failed} 份文件處理失敗，可在文件頁重新索引。")
            : pending + indexing == 0 ? (SubsystemStates.Idle, "沒有待處理的文件。")
            : (SubsystemStates.Healthy, $"{pending + indexing} 份文件處理中。");
        IReadOnlyList<SubsystemFact> facts = [
            SubsystemFact.Count("pending", "等待處理", pending),
            SubsystemFact.Count("indexing", "處理中", indexing, indexing > 0 ? SubsystemTones.Positive : SubsystemTones.Neutral),
            SubsystemFact.Count("failed", "處理失敗", failed, failed > 0 ? SubsystemTones.Danger : SubsystemTones.Neutral),
            SubsystemFact.Words("retrieval", "檢索方式", embedding.Enabled ? "語意向量" : "關鍵字"),
        ];
        return new(status, summary, facts);
    }
}
