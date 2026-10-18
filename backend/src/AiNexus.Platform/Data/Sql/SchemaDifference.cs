namespace AiNexus.Platform.Data.Sql;

/// <summary><see cref="ModelSchema"/> 找到的一項 EF 模型與資料庫結構差異。</summary>
/// <param name="Kind">差異種類。</param>
/// <param name="Target">表或檢視（<c>schema.name</c>），欄位差異再加上 <c>.column</c>。</param>
/// <param name="Detail">兩邊各是什麼。</param>
/// <param name="Blocking">
/// 程式會因此失敗：EF 讀寫的表或欄位不存在、型別或 nullable 不同，或資料庫多出 NOT NULL 又沒有預設值的欄位（EF 新增列時會失敗）。
/// 其他差異（資料庫先加的欄位、只經 SQL 存取的表）只需提醒；資料庫可以先於程式加欄位（ADR 0011）。
/// </param>
public sealed record SchemaDifference(SchemaDifferenceKind Kind, string Target, string Detail, bool Blocking)
{
    public override string ToString() => $"[{Kind}] {Target}：{Detail}";
}
