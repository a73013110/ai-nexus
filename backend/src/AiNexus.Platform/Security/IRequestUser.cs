namespace AiNexus.Platform.Security;

/// <summary>目前請求中，由身分模組成功解析出的已登入使用者。</summary>
/// <remarks>基礎設施只用它關聯紀錄（如日誌、分散式追蹤）；授權判斷絕不可直接依賴此欄位。</remarks>
public interface IRequestUser
{
    Guid? ResolvedId { get; }
}
