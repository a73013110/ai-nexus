using AiNexus.Platform.Errors;

namespace AiNexus.Features.Platform.RemoteFiles;

/// <summary>遠端檔案的預期失敗。</summary>
public static class RemoteFilesErrors
{
    /// <summary>
    /// 憑證不對（被竄改、已過期、發給別人），或來源說沒有這個檔案。不分開回報：分開等於告訴持有別人憑證的人「這張是有效的」。
    /// 過期多半是對話框開太久，前端重新讀取表單即可拿到新憑證。
    /// </summary>
    public static readonly Error NotFound = Error.NotFound("remote_file_not_found");

    /// <summary>來源系統未設定、連不上、逾時或回應異常。</summary>
    public static readonly Error Unavailable = Error.Unavailable("remote_file_unavailable");

    /// <summary>超過來源設定的大小上限（例如 <c>Gd:MaxFileBytes</c>）。</summary>
    public static readonly Error TooLarge = Error.TooLarge("remote_file_too_large");
}
