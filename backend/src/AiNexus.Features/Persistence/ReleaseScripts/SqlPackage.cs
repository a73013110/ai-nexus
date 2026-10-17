using System.ComponentModel;
using System.Diagnostics;

namespace AiNexus.Features.Persistence.ReleaseScripts;

/// <summary>執行 Microsoft 的 SqlPackage。</summary>
/// <remarks>
/// 連線字串（含密碼）透過私有暫存資料夾中的 response file 傳入，執行後立即刪除。絕不放上命令列，以免被其他程序讀到。
/// </remarks>
/// <param name="executable">
/// SqlPackage 的執行檔或 <c>.dll</c>。為 <see langword="null"/> 時在工作目錄執行 <c>dotnet tool run sqlpackage</c>，
/// 版本由 repo 的 <c>.config/dotnet-tools.json</c> 固定。
/// </param>
public sealed class SqlPackage(string? executable)
{
    /// <summary>
    /// 把 <paramref name="connectionString"/> 指向的資料庫匯出到 <paramref name="target"/>：預設是 <c>.dacpac</c> 檔；
    /// <paramref name="perObject"/> 為 true 時則是每個物件一份腳本的資料夾。取消 <paramref name="ct"/> 會終止 SqlPackage。
    /// </summary>
    public Task ExtractAsync(string connectionString, string target, bool perObject, CancellationToken ct)
        => RunAsync("Extract", connectionString, [$"/TargetFile:{target}", $"/p:ExtractTarget={(perObject ? "SchemaObjectType" : "DacPac")}"], ct);

    /// <summary>
    /// 產生 response file 中的一個參數。SqlPackage 以空白切分參數（雙引號內除外），並把 <c>\"</c> 當成引號；其他反斜線都是字面字元。
    /// 它解析失敗時會把整個參數印出來，等於洩漏密碼，所以值一律加引號、跳脫引號，且不能以反斜線結尾。
    /// </summary>
    public static string ResponseFileArgument(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        if (value.Contains('\r', StringComparison.Ordinal) || value.Contains('\n', StringComparison.Ordinal))
            throw new ReleaseScriptException($"SqlPackage 的 {name} 不能含換行。");
        // 結尾反斜線後接的結束引號會被當成跳脫字元，所以補一個分號（連線字串本來就可以以分號結尾）。
        if (value.EndsWith('\\')) value += ";";
        return $"/{name}:\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }

    private async Task RunAsync(string action, string connectionString, IReadOnlyList<string> arguments, CancellationToken ct)
    {
        // CreateTempSubdirectory 建立的資料夾只有目前使用者能開啟（Windows 為使用者自己的 %TEMP%，其他系統為 0700），避免密碼外洩給其他使用者。
        var folder = Directory.CreateTempSubdirectory("AiNexus-sqlpackage-");
        try
        {
            var response = Path.Combine(folder.FullName, "connection.rsp");
            await File.WriteAllTextAsync(response, ResponseFileArgument("SourceConnectionString", connectionString), ct);
            var start = executable is null ? new ProcessStartInfo("dotnet") { ArgumentList = { "tool", "run", "sqlpackage", "--" } }
                : executable.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? new ProcessStartInfo("dotnet") { ArgumentList = { executable } }
                : new ProcessStartInfo(executable);
            start.UseShellExecute = false;
            foreach (var argument in (string[])[$"/Action:{action}", "@" + response, .. arguments]) start.ArgumentList.Add(argument);

            using var process = Start(start);
            try { await process.WaitForExitAsync(ct); }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                throw;
            }
            if (process.ExitCode != 0)
                throw new ReleaseScriptException($"SqlPackage {action} 失敗（結束碼 {process.ExitCode}）；原因見上方輸出。{(executable is null ? "若是找不到工具，先在 repo 根目錄執行 dotnet tool restore。" : "")}");
        }
        finally { folder.Delete(recursive: true); }
    }

    private Process Start(ProcessStartInfo start)
    {
        try { return Process.Start(start) ?? throw new ReleaseScriptException($"無法啟動 SqlPackage：{start.FileName}"); }
        catch (Win32Exception ex)
        {
            throw new ReleaseScriptException($"找不到 SqlPackage（{executable ?? "dotnet"}）：{ex.Message}。版本固定在 .config/dotnet-tools.json，請在 repo 根目錄執行 dotnet tool restore，或以 --sqlpackage 指定執行檔。", ex);
        }
    }
}
