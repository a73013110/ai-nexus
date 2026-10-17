namespace AiNexus.Features.Persistence.ReleaseScripts;

/// <summary>發布 SQL 檔的分類：結構（DDL）放在 schema/，資料列（DML）放在 data/，兩者由不同的 DBA 負責執行。</summary>
public enum ReleaseScriptKind
{
    Schema,
    Data,
}
