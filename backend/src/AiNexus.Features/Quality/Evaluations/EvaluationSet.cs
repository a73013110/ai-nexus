using AiNexus.Features.Collaboration;
using AiNexus.Features.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Quality.Evaluations;

/// <summary>版本化的題庫。名稱、擁有者與分享設定存放在相同 id 的 <see cref="WorkspaceResource"/>，這裡只存題目與版本。</summary>
[Comment("評測題庫、固定測試案例與版本。")]
public sealed class EvaluationSet
{
    public const string Kind = "evaluation";

    [Comment("資料的主鍵識別碼。")]
    public Guid Id { get; set; }
    [Comment("業務物件的用途說明。")]
    public string Description { get; set; } = "";
    [Comment("固定評測案例的 JSON 快照。")]
    public string CasesJson { get; set; } = "[]";
    [Comment("業務版本號，用於歷史或樂觀並行控制。")]
    public int Version { get; set; } = 1;

    public EvaluationSetDto ToDto(ResourceDto resource) => new(resource, Description, EvaluationJson.Parse<EvaluationCase>(CasesJson), Version);
}

internal static class EvaluationSetQueries
{
    /// <summary>以操作者的權限讀取題組；無讀取權限時直接回傳錯誤。</summary>
    public static async Task<Result<EvaluationSetDto>> LoadSetAsync(this ResourceAccess access, NexusDbContext db, Guid actor, Guid id, CancellationToken ct)
    {
        var resource = await access.RequireAsync(actor, id, EvaluationSet.Kind, ct);
        if (!resource.IsSuccess) return resource.Error;
        var set = await db.Set<EvaluationSet>().AsNoTracking().SingleAsync(x => x.Id == id, ct);
        return set.ToDto(await access.DescribeAsync(actor, resource.Value, ct));
    }
}

internal sealed class EvaluationSetConfiguration : IEntityTypeConfiguration<EvaluationSet>
{
    public void Configure(EntityTypeBuilder<EvaluationSet> s)
    {
        s.ToTable("EvaluationSets", "quality"); s.HasKey(x => x.Id); s.Property(x => x.Description).HasMaxLength(2000);
    }
}
