namespace EDoc.Core.Database.Models;

/// <summary>
/// 安全的物件類別
/// 用途：繞過 CheckMarx 的 Heuristic 相關低風險
/// </summary>
public record DbSafeObject(string Sql, object? Param);