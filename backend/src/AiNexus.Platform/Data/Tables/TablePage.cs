namespace AiNexus.Platform.Data.Tables;

/// <summary>
/// 表格的一頁。<paramref name="Complete"/> 為 true 時 <paramref name="Items"/> 是符合查詢條件的全部資料、尚未套用欄位篩選與分頁，
/// 前端之後的排序、篩選與換頁都在本機完成；否則是已篩選、排序後的第 <paramref name="Offset"/> 筆起的一頁，<paramref name="Total"/> 是篩選後的總數。
/// </summary>
public sealed record TablePage<T>(IReadOnlyList<T> Items, int Total, int Offset, bool Complete);
