using AiNexus.Data.App;
using AiNexus.Platform.Modules;

namespace AiNexus.Features.Work.Jobs.Retention;

/// <summary>
/// 還指著佇列工作的資料，由擁有那些資料的模組實作：保存期清理（<see cref="JobRetentionTask"/>）不刪仍被引用的工作。
/// 資料表記了工作 Id 就要宣告，不論有沒有外鍵：有外鍵的不宣告，刪除會被資料庫擋下、整個清理任務失敗（<c>JobRetentionTests</c> 檢查）；
/// 沒有外鍵的不宣告，工作刪掉後那筆資料就查不到進度與失敗原因。
/// </summary>
[RegisterImplementations(typeof(JobReferenceRegistration), nameof(JobReferenceRegistration.AddJobReferences))]
public interface IJobReferences
{
    /// <summary>仍被引用的工作 Id。回傳查詢、不要先讀進記憶體：它會組進刪除語句的子查詢。</summary>
    IQueryable<Guid?> JobIds(AppDbContext db);
}

public static class JobReferenceRegistration
{
    /// <summary>註冊工作引用的來源（Scoped）。</summary>
    public static IServiceCollection AddJobReferences<TReferences>(this IServiceCollection services) where TReferences : class, IJobReferences
        => services.AddScoped<IJobReferences, TReferences>();
}
