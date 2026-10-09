using Microsoft.EntityFrameworkCore;

namespace AiNexus.Features.Identity.Users;

[Comment("使用者個人外觀、閱讀、對話操作與通知偏好；不含服務密鑰。")]
public sealed class UserPreferences
{
    public const int DefaultReadingFontSize = 15;
    public const double DefaultReadingLineHeight = 1.2;
    public const int DefaultSidebarWidth = 240;
    [Comment("外觀偏好：system、light 或 dark。")]
    public string Theme { get; set; } = "system";
    [Comment("是否減少動畫與動態效果。")]
    public bool ReducedMotion { get; set; }
    [Comment("偏好的核准模型識別碼；空值使用伺服器預設。")]
    public string? DefaultModelId { get; set; }
    [Comment("對話文字大小，以 CSS px 的偏好值記錄。")]
    public int ReadingFontSize { get; set; } = DefaultReadingFontSize;
    [Comment("對話閱讀行高倍率。")]
    public double ReadingLineHeight { get; set; } = DefaultReadingLineHeight;
    [Comment("介面密度偏好。")]
    public string Density { get; set; } = "comfortable";
    [Comment("側欄寬度偏好。")]
    public int SidebarWidth { get; set; } = DefaultSidebarWidth;
    [Comment("閱讀區寬度偏好。")]
    public string ReadingWidth { get; set; } = "standard";
    [Comment("是否以 Enter 送出提問；IME 組字不送出。")]
    public bool EnterToSend { get; set; } = true;
    [Comment("生成時是否跟隨最新回答。")]
    public bool AutoFollow { get; set; } = true;
    [Comment("是否在瀏覽器按使用者保存草稿。")]
    public bool SaveLocalDrafts { get; set; } = true;
    [Comment("是否在背景分頁提醒回答完成。")]
    public bool NotifyOnCompletion { get; set; }
    [Comment("偏好的推理強度。")]
    public string DefaultReasoningEffort { get; set; } = "auto";
}
