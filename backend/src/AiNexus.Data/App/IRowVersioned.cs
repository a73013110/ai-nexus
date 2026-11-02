using Microsoft.EntityFrameworkCore;

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
    /// 內容完全沒變時 EF 不送 UPDATE，也就不比對；此時沒有東西會被蓋掉，所以不算衝突。
    /// </summary>
    public static void ExpectRowVersion<T>(this DbContext db, T entity, long version) where T : class, IRowVersioned
        => db.Entry(entity).Property<long>(nameof(IRowVersioned.RowVersion)).OriginalValue = version;
}
