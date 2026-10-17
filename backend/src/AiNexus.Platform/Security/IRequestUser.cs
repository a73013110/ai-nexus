namespace AiNexus.Platform.Security;

/// <summary>當前請求中由身分識別模組成功解析的已登入使用者資訊。</summary>
/// <remarks>基礎設施層僅將其用於關聯性記錄（如日誌、分散式追蹤）；商業邏輯授權決策絕不可直接依賴此欄位。</remarks>
public interface IRequestUser
{
    Guid? ResolvedId { get; }
}
