using AiNexus.Data.App;
using AiNexus.Data.App.Quality;
using AiNexus.Features.Collaboration;
using Microsoft.EntityFrameworkCore;
using AiNexus.Platform.Errors;

namespace AiNexus.Features.Quality.Evaluations;

internal static class EvaluationSetQueries
{
    /// <summary>以操作者的權限讀取題組；無讀取權限時直接回傳錯誤。</summary>
    public static async Task<Result<EvaluationSetDto>> LoadSetAsync(this ResourceAccess access, AppDbContext db, Guid actor, Guid id, CancellationToken ct)
    {
        var resource = await access.RequireAsync(actor, id, EvaluationSet.Kind, ct);
        if (!resource.IsSuccess) return resource.Error;
        var set = await db.Set<EvaluationSet>().AsNoTracking().SingleAsync(x => x.Id == id, ct);
        return set.ToDto(await access.DescribeAsync(actor, resource.Value, ct));
    }
}
