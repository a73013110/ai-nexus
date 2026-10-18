namespace AiNexus.Platform.Data.Sql;

/// <summary><see cref="SchemaDifference"/> 的種類。</summary>
public enum SchemaDifferenceKind
{
    /// <summary>EF 對應的表或檢視不存在。</summary>
    MissingObject,
    /// <summary>EF 對應的欄位不存在。</summary>
    MissingColumn,
    /// <summary>欄位型別、長度、精度或向量維度不同。</summary>
    TypeMismatch,
    /// <summary>欄位可否為 null 不同。</summary>
    NullabilityMismatch,
    /// <summary>資料庫有、EF 沒有對應的欄位。</summary>
    UnmappedColumn,
    /// <summary>資料庫有、EF 沒有對應的表或檢視（只經 SQL 存取的表）。</summary>
    UnmappedObject,
}
