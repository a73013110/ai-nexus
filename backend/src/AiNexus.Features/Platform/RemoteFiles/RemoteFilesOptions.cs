using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Platform.RemoteFiles;

/// <summary>遠端檔案憑證的設定，區段為 <c>RemoteFiles</c>。</summary>
public sealed class RemoteFilesOptions
{
    public const string Section = "RemoteFiles";

    /// <summary>
    /// 憑證有效分鐘數。短一點，憑證外流（貼到別處、留在瀏覽紀錄）時能用的時間就短；
    /// 太短則對話框開著沒看就失效，要重新開啟表單。
    /// </summary>
    [Range(1, 720)] public int TicketMinutes { get; set; } = 30;
}

[OptionsValidator]
public sealed partial class RemoteFilesOptionsValidator : IValidateOptions<RemoteFilesOptions>;
