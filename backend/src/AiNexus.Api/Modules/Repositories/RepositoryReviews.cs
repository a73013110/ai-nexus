using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AiNexus.BuildingBlocks;
using AiNexus.Modules.AccessControl;
using AiNexus.Modules.Administration;
using AiNexus.Modules.Identity;
using AiNexus.Modules.Inference;
using AiNexus.Modules.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Modules.Repositories;

public sealed class RepositoryReview
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public Guid JobId { get; set; }
    public string BaseUrl { get; set; } = "";
    public string Repository { get; set; } = "";
    public string Commit { get; set; } = "";
    public string? BaseCommit { get; set; }
    public string ModelId { get; set; } = "";
    public string ConfigurationFingerprint { get; set; } = "";
    public string Note { get; set; } = "";
    public string IdempotencyKey { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public string SnapshotJson { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class RepositoryReviewResult
{
    public Guid ReviewId { get; set; }
    public int Ordinal { get; set; }
    public string Output { get; set; } = "";
    public bool Truncated { get; set; }
    public long? InputTokens { get; set; }
    public long? OutputTokens { get; set; }
    public long ElapsedMs { get; set; }
}
public sealed record CreateRepositoryReviewRequest(string Repository, string Commit, string? BaseCommit, string? ModelId, string? Note, string IdempotencyKey, string Purpose = "review");
public sealed record RepositoryReviewDto(Guid Id, string Repository, string Commit, string? BaseCommit, string ModelId, string Note, DateTimeOffset CreatedAt, JobDto Job, string Purpose = "review");
public sealed record RepositoryReviewSectionDto(int Ordinal, string Label, string Diff, bool Binary, string? Output, bool Truncated, long? InputTokens, long? OutputTokens, long? ElapsedMs);
public sealed record RepositoryReviewReportDto(string Output, bool Truncated, long? InputTokens, long? OutputTokens, long ElapsedMs);
public sealed record RepositoryReviewDetailDto(RepositoryReviewDto Review, IReadOnlyList<RepositoryReviewSectionDto> Sections, RepositoryReviewReportDto? Report = null, int Version = 1);

// Both the source and the model configuration are frozen before the durable job is enqueued.
public sealed class RepositoryReviewService(NexusDbContext db, RepositoryService repositories, JobService jobs, RepositoryWriteLock writes,
    ModelCatalog catalog, ModelPresentation presentation, ModelPolicyService policy, IOptions<InferenceOptions> inference)
{
    public async Task<RepositoryReviewDto> CreateAsync(Guid owner, CreateRepositoryReviewRequest request, CancellationToken ct)
    {
        RepositoryService.RepositoryRoute(request.Repository);
        RepositoryService.Commit(request.Commit);
        var basis = string.IsNullOrWhiteSpace(request.BaseCommit) ? null : request.BaseCommit.ToLowerInvariant();
        if (basis is not null) RepositoryService.Commit(basis);
        var head = request.Commit.ToLowerInvariant();
        if (basis == head) throw new ApiException(400, "review_empty_range", "起點與終點必須是不同的 commit。");
        var note = request.Note?.Trim() ?? "";
        var purpose = RepositoryReviewPlan.Purpose(request.Purpose);
        if (note.Length > 2000 || !Guid.TryParse(request.IdempotencyKey, out _)) throw new ApiException(400, "review_request_invalid", "Review 重點最多 2,000 字元，請使用有效的請求識別碼。");
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { request.Repository, head, basis, request.ModelId, note, purpose })));
        var legacyHash = purpose == "review" ? Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { request.Repository, head, basis, request.ModelId, note }))) : null;
        bool SameRequest(RepositoryReview existing) => existing.RequestHash == hash ||
            (existing.RequestHash == legacyHash && Snapshot(existing).Version == 1);
        await writes.Gate.WaitAsync(ct);
        try
        {
            var existing = await db.Set<RepositoryReview>().AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == owner && x.IdempotencyKey == request.IdempotencyKey, ct);
            if (existing is not null)
            {
                if (!SameRequest(existing)) throw new ApiException(409, "idempotency_conflict", "請求識別碼已用於不同的 review。");
                await RequireSourceAsync(owner, existing, ct);
                return await DescribeAsync(existing, ct);
            }
            var model = await catalog.RequireAsync(request.ModelId, ct);
            await policy.RequireAsync(owner, model.Id, ct);
            await repositories.RequireCommitAsync(owner, request.Repository, head, ct);
            if (basis is not null) await repositories.RequireCommitAsync(owner, request.Repository, basis, ct);
            var diff = await repositories.DiffAsync(owner, request.Repository, head, basis, ct);
            var snapshot = RepositoryReviewPlan.Create(diff, model, request.Repository, head, basis, note, purpose);
            var row = new RepositoryReview { OwnerId = owner, Repository = request.Repository, Commit = head, BaseCommit = basis,
                BaseUrl = repositories.BaseUrl, ModelId = model.Id, Note = note, ConfigurationFingerprint = ModelTaskConfiguration.Capture(model, inference.Value).Fingerprint,
                SnapshotJson = JsonSerializer.Serialize(snapshot), IdempotencyKey = request.IdempotencyKey, RequestHash = hash };
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            // This account lock and unique request key also protect multiple application hosts.
            await db.Users.Where(x => x.Id == owner).ExecuteUpdateAsync(p => p.SetProperty(x => x.LastSeenAt, x => x.LastSeenAt), ct);
            var duplicate = await db.Set<RepositoryReview>().AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == owner && x.IdempotencyKey == request.IdempotencyKey, ct);
            if (duplicate is not null)
            {
                if (!SameRequest(duplicate)) throw new ApiException(409, "idempotency_conflict", "請求識別碼已用於不同的 review。");
                return await DescribeAsync(duplicate, ct);
            }
            row.JobId = jobs.Enqueue(owner, null, row.Id, "repository-review", request.Repository + " · " + head[..10]).Id;
            db.Add(row);
            db.AuditEvents.Add(new() { OwnerId = owner, ResourceId = row.Id, Action = "repository.review.created", Result = "queued" });
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            return await DescribeAsync(row, ct);
        }
        finally { writes.Gate.Release(); }
    }
    public async Task<IReadOnlyList<RepositoryReviewDto>> ListAsync(Guid owner, string? repository, CancellationToken ct)
    {
        if (repository is not null) RepositoryService.RepositoryRoute(repository);
        var rows = await db.Set<RepositoryReview>().AsNoTracking().Where(x => x.OwnerId == owner && x.BaseUrl == repositories.BaseUrl && (repository == null || x.Repository == repository))
            .OrderByDescending(x => x.CreatedAt).Take(30).ToListAsync(ct);
        var result = new List<RepositoryReviewDto>();
        foreach (var row in rows) result.Add(await DescribeAsync(row, ct));
        return result;
    }
    public async Task<RepositoryReviewDetailDto> DetailAsync(Guid owner, Guid id, CancellationToken ct)
    {
        var row = await OwnedAsync(owner, id, ct); await RequireSourceAsync(owner, row, ct);
        var results = await db.Set<RepositoryReviewResult>().AsNoTracking().Where(x => x.ReviewId == id).ToDictionaryAsync(x => x.Ordinal, ct);
        var snapshot = Snapshot(row);
        results.TryGetValue(RepositoryReviewPlan.ReportOrdinal, out var report);
        return new(await DescribeAsync(row, ct), snapshot.Slices.Select((slice, i) => {
            results.TryGetValue(i, out var result);
            return new RepositoryReviewSectionDto(i, slice.Label, slice.Diff, slice.Binary, result?.Output, result?.Truncated ?? false, result?.InputTokens, result?.OutputTokens, result?.ElapsedMs);
        }).ToArray(), report is null ? null : new(report.Output, report.Truncated, report.InputTokens, report.OutputTokens, report.ElapsedMs), snapshot.Version);
    }
    public async Task<JobDto> ChangeJobAsync(Guid owner, Guid id, bool retry, CancellationToken ct)
    {
        var row = await OwnedAsync(owner, id, ct);
        return retry ? await jobs.RetryAsync(owner, row.JobId, ct) : await jobs.CancelAsync(owner, row.JobId, ct);
    }
    public async Task<RepositoryReview> OwnedAsync(Guid owner, Guid id, CancellationToken ct) => await db.Set<RepositoryReview>().AsNoTracking()
        .SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == owner, ct) ?? throw new ApiException(404, "review_not_found", "找不到這份 review。");
    public async Task RequireSourceAsync(Guid owner, RepositoryReview row, CancellationToken ct)
    {
        if (row.BaseUrl != repositories.BaseUrl) throw new ApiException(409, "review_host_changed", "程式庫主機已變更，請在目前的連線建立新的 review。");
        await repositories.RequireCommitAsync(owner, row.Repository, row.Commit, ct);
        if (row.BaseCommit is not null) await repositories.RequireCommitAsync(owner, row.Repository, row.BaseCommit, ct);
    }
    private async Task<RepositoryReviewDto> DescribeAsync(RepositoryReview x, CancellationToken ct) => new(x.Id, x.Repository, x.Commit, x.BaseCommit,
        presentation.PublicId(x.ModelId), x.Note, x.CreatedAt, JobService.Describe(await db.Set<BackgroundJob>().AsNoTracking().SingleAsync(j => j.Id == x.JobId, ct)), Snapshot(x).Purpose);
    public static ReviewSnapshot Snapshot(RepositoryReview row) => JsonSerializer.Deserialize<ReviewSnapshot>(row.SnapshotJson) is { Version: 1 or 2 } snapshot
        ? snapshot : throw new ApiException(409, "review_snapshot_unsupported", "此 review 快照版本目前無法處理，請建立新的 review。");
    public static ReviewSlice[] Split(string diff, int budget)
    {
        if (string.IsNullOrWhiteSpace(diff)) throw new ApiException(400, "review_no_changes", "此 commit 或區間沒有可檢閱的變更。");
        if (Encoding.UTF8.GetByteCount(diff) > 256000) throw new ApiException(413, "repository_diff_limit", "變更超過 256 KB，請縮小區間。");
        var files = System.Text.RegularExpressions.Regex.Split(diff.Replace("\r\n", "\n"), "(?=^diff --git )", System.Text.RegularExpressions.RegexOptions.Multiline).Where(x => !string.IsNullOrWhiteSpace(x));
        var slices = new List<ReviewSlice>();
        foreach (var file in files)
        {
            var label = string.Concat(file.Split('\n')[0].Take(500));
            var binary = file.Contains("GIT binary patch", StringComparison.Ordinal) || file.Contains("Binary files ", StringComparison.Ordinal);
            var parts = RepositoryReviewPlan.Chunks(file, budget).ToArray();
            for (var i = 0; i < parts.Length; i++)
            {
                slices.Add(new(label + (parts.Length > 1 ? $" · 區段 {i + 1}" : ""), parts[i], binary));
                if (slices.Count > 60) throw new ApiException(413, "review_segment_limit", "變更需要超過 60 個區段，請縮小區間或選擇更大的模型。");
            }
        }
        return slices.ToArray();
    }
}

public sealed class RepositoryReviewHandler(NexusDbContext db, RepositoryReviewService reviews, AccessService features, ModelTaskService model,
    ModelCatalog catalog, ModelPresentation presentation, IOptions<InferenceOptions> inference) : IBackgroundJobHandler
{
    public string Kind => "repository-review";
    private async Task<RepositoryReview> RequireAsync(BackgroundJob job, CancellationToken ct)
    {
        if (!(await features.ForUserAsync(job.OwnerId, ct)).Features.Any(x => x.Id == "repositories")) throw new ApiException(403, "review_access_revoked", "你的程式庫功能已停用。");
        var row = await reviews.OwnedAsync(job.OwnerId, job.SubjectId, ct);
        await reviews.RequireSourceAsync(job.OwnerId, row, ct); return row;
    }
    public async Task ValidateRetryAsync(BackgroundJob job, CancellationToken ct)
    {
        var row = await RequireAsync(job, ct);
        ModelTaskConfiguration.Require(await catalog.RequireAsync(presentation.PublicId(row.ModelId), ct), inference.Value, row.ConfigurationFingerprint);
    }
    public async Task ExecuteAsync(JobExecution execution, CancellationToken ct)
    {
        await ValidateRetryAsync(execution.Job, ct);
        var row = await RequireAsync(execution.Job, ct); var snapshot = RepositoryReviewService.Snapshot(row); var slices = snapshot.Slices;
        var results = await db.Set<RepositoryReviewResult>().AsNoTracking().Where(x => x.ReviewId == row.Id).ToDictionaryAsync(x => x.Ordinal, ct);
        var modern = snapshot.Version == 2;
        if (modern && (snapshot.Direct || slices.All(x => x.Binary)))
        {
            if (results.ContainsKey(RepositoryReviewPlan.ReportOrdinal)) return;
            await execution.CheckpointAsync("正在分析整體變更", 0, 1, ct);
            var binary = slices.All(x => x.Binary);
            await GenerateAsync(execution, row, RepositoryReviewPlan.ReportOrdinal, RepositoryReviewPlan.Source(slices), snapshot.ReportInstruction!,
                "untrusted_diff", "整體報告已完成", 1, 1, ct, maxOutputTokens: 1200,
                answer: binary ? new("此變更只有二進位內容，無法由文字模型判定是否有問題，請人工確認原檔。\n" + RepositoryReviewPlan.Source(slices), false, null, null) : null);
            return;
        }
        var completed = results.Keys.Count(x => x >= 0 && x < slices.Length);
        var total = slices.Length + (modern ? 1 : 0);
        for (var i = 0; i < slices.Length; i++)
        {
            if (results.ContainsKey(i)) continue;
            await execution.CheckpointAsync($"檢閱區段 {i + 1} / {slices.Length}", completed, total, ct);
            var slice = slices[i];
            results[i] = await GenerateAsync(execution, row, i, $"區段：{slice.Label}\n{slice.Diff}", snapshot.Instruction,
                "untrusted_diff", $"已分析 {++completed} / {slices.Length} 區段", completed, total, ct,
                maxOutputTokens: modern ? 512 : null,
                answer: slice.Binary ? new("二進位變更未送交文字模型檢閱，請人工確認原檔。", false, null, null) : null);
        }
        if (!modern || results.ContainsKey(RepositoryReviewPlan.ReportOrdinal)) return;

        // Intermediate reductions have deterministic ordinals after the source slices, so retries reuse their checkpoints too.
        var evidence = slices.Select((x, i) => $"來源區段 {i + 1}：{x.Label}\n{results[i].Output}" +
            (results[i].Truncated ? "\n注意：此區段分析已達輸出上限，檢閱不完整。" : "")).ToArray();
        var batches = RepositoryReviewPlan.Batches(evidence, snapshot.InputBudget);
        var ordinal = slices.Length;
        var truncated = results.Values.Any(x => x.Truncated);
        for (var level = 1; batches.Length > 1; level++)
        {
            var reduced = new List<string>();
            for (var i = 0; i < batches.Length; i++, ordinal++)
            {
                if (!results.TryGetValue(ordinal, out var result))
                {
                    await execution.CheckpointAsync($"正在彙整整體報告 · 第 {level} 輪 {i + 1} / {batches.Length}", completed, total, ct);
                    result = await GenerateAsync(execution, row, ordinal, batches[i], snapshot.ReductionInstruction!, "untrusted_analysis",
                        "正在彙整整體報告", completed, total, ct, maxOutputTokens: Math.Clamp(snapshot.InputBudget / 12, 32, 384), byteLimit: snapshot.InputBudget / 3);
                    results[ordinal] = result;
                }
                truncated |= result.Truncated; reduced.Add(result.Output);
            }
            batches = RepositoryReviewPlan.Batches(reduced, snapshot.InputBudget);
        }
        await execution.CheckpointAsync("正在產生整體報告", completed, total, ct);
        await GenerateAsync(execution, row, RepositoryReviewPlan.ReportOrdinal, batches.Single(), snapshot.ReportInstruction!, "untrusted_analysis",
            "整體報告已完成", total, total, ct, maxOutputTokens: 1200, truncated: truncated);
    }

    private async Task<RepositoryReviewResult> GenerateAsync(JobExecution execution, RepositoryReview row, int ordinal, string text, string instruction,
        string sourceTag, string stage, int completed, int total, CancellationToken ct, int? maxOutputTokens = null, bool truncated = false,
        int? byteLimit = null, ModelTaskResult? answer = null)
    {
        await RequireAsync(execution.Job, ct);
        var timer = Stopwatch.StartNew();
        answer ??= await model.GenerateAsync(row.OwnerId, Kind,
            RepositoryReviewPlan.Context(row.Repository, row.Commit, row.BaseCommit, row.Note) + $"<{sourceTag}>\n{text}\n</{sourceTag}>",
            instruction, ct, presentation.PublicId(row.ModelId), expectedConfiguration: row.ConfigurationFingerprint, maxOutputTokens: maxOutputTokens);
        if (byteLimit is not null && Encoding.UTF8.GetByteCount(answer.Text) > byteLimit)
            throw new ApiException(502, "review_summary_too_long", "模型未能將分析彙整到上下文預算內，已保留完成區段。請重試，或縮小範圍、改用其他模型建立 review。");
        await RequireAsync(execution.Job, ct);
        var result = new RepositoryReviewResult { ReviewId = row.Id, Ordinal = ordinal, Output = answer.Text, Truncated = truncated || answer.Truncated,
            InputTokens = answer.InputTokens, OutputTokens = answer.OutputTokens, ElapsedMs = timer.ElapsedMilliseconds };
        db.Add(result);
        await execution.CheckpointAsync(stage, completed, total, ct);
        return result;
    }
}
public static class RepositoryReviewConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        var r = model.Entity<RepositoryReview>(); r.ToTable("RepositoryReviews", "workspace"); r.HasKey(x => x.Id);
        r.Property(x => x.BaseUrl).HasMaxLength(500); r.Property(x => x.Repository).HasMaxLength(201); r.Property(x => x.Commit).HasMaxLength(64); r.Property(x => x.BaseCommit).HasMaxLength(64);
        r.Property(x => x.ModelId).HasMaxLength(160); r.Property(x => x.ConfigurationFingerprint).HasMaxLength(64); r.Property(x => x.Note).HasMaxLength(2000);
        r.Property(x => x.IdempotencyKey).HasMaxLength(80); r.Property(x => x.RequestHash).HasMaxLength(64); r.Property(x => x.SnapshotJson).HasMaxLength(1000000);
        r.HasIndex(x => new { x.OwnerId, x.IdempotencyKey }).IsUnique(); r.HasIndex(x => new { x.OwnerId, x.CreatedAt });
        r.HasOne<NexusUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        r.HasOne<BackgroundJob>().WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Restrict);
        var s = model.Entity<RepositoryReviewResult>(); s.ToTable("RepositoryReviewResults", "workspace"); s.HasKey(x => new { x.ReviewId, x.Ordinal });
        s.Property(x => x.Output).HasMaxLength(64000); s.HasOne<RepositoryReview>().WithMany().HasForeignKey(x => x.ReviewId).OnDelete(DeleteBehavior.Restrict);
    }
}
