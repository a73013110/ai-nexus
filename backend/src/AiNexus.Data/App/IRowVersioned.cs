using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AiNexus.Data.App;

/// <summary>
/// 有 rowversion 的實體：讀取時把版本交給前端，寫入時帶回來，資料在這段期間被別人改過就拒絕，不會悄悄覆蓋。
/// 用 <see cref="long"/> 對應（組態 <c>IsRowVersion().HasConversion&lt;byte[]&gt;()</c>）：JSON 與查詢字串都能直接傳；它是資料庫層級的遞增計數，碰不到 JavaScript 數字的安全上限。
/// </summary>
public interface IRowVersioned
{
    long RowVersion { get; set; }
}

public static class RowVersionedExtensions
{
    /// <summary>
    /// 要求 <paramref name="entity"/> 的版本還是 <paramref name="version"/>（前端讀到的值）：SaveChanges 以它比對，不符時拋 <see cref="DbUpdateConcurrencyException"/>。
    /// 一定會送 UPDATE：只改子表或內容完全沒變時，主列也要比對並升版，前端才看得出整筆表單變過。
    /// </summary>
    public static void ExpectRowVersion<T>(this DbContext db, T entity, long version) where T : class, IRowVersioned
    {
        var entry = db.Entry(entity);
        entry.Property<long>(nameof(IRowVersioned.RowVersion)).OriginalValue = version;
        // 不能看實體狀態：版本不同時 EF 會把 rowversion 標成已修改、狀態變 Modified，但資料庫產生的欄位不會寫入，
        // 沒有別的欄位要寫就不送 UPDATE，舊版本反而悄悄成功。所以一律把一個一般欄位標成已修改（值不變）。
        entry.Properties.First(x => x.Metadata is { ValueGenerated: ValueGenerated.Never, IsConcurrencyToken: false } && !x.Metadata.IsPrimaryKey()).IsModified = true;
    }
}
