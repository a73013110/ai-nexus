using System.ComponentModel;
using System.Diagnostics;

namespace AiNexus.Features.Persistence.ReleaseScripts;

/// <summary>
/// Runs Microsoft's SqlPackage. The connection string, password included, goes through a response file in a private
/// temporary folder that is deleted right after the run, never on a command line other processes can read.
/// </summary>
/// <param name="executable">
/// SqlPackage's executable or <c>.dll</c>; <see langword="null"/> runs <c>dotnet tool run sqlpackage</c> in the working
/// directory, which is the version pinned in the repository's <c>.config/dotnet-tools.json</c>.
/// </param>
public sealed class SqlPackage(string? executable)
{
    /// <summary>
    /// Extracts the database <paramref name="connectionString"/> opens into <paramref name="target"/>: a <c>.dacpac</c>
    /// file, or with <paramref name="perObject"/> a folder with one script per object. <paramref name="ct"/> kills SqlPackage.
    /// </summary>
    public Task ExtractAsync(string connectionString, string target, bool perObject, CancellationToken ct)
        => RunAsync("Extract", connectionString, [$"/TargetFile:{target}", $"/p:ExtractTarget={(perObject ? "SchemaObjectType" : "DacPac")}"], ct);

    /// <summary>
    /// One response-file argument. SqlPackage splits response files on white space outside double quotes and reads
    /// <c>\"</c> as a quote; other backslashes are literal. It prints an argument it cannot parse, so a mistake here
    /// would show the password: the value is quoted, its quotes escaped, and it never ends in a backslash.
    /// </summary>
    public static string ResponseFileArgument(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        if (value.Contains('\r', StringComparison.Ordinal) || value.Contains('\n', StringComparison.Ordinal))
            throw new ReleaseScriptException($"SqlPackage 的 {name} 不能含換行。");
        // A closing quote after a trailing backslash would read as an escaped quote; a connection string may end in ';'.
        if (value.EndsWith('\\')) value += ";";
        return $"/{name}:\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }

    private async Task RunAsync(string action, string connectionString, IReadOnlyList<string> arguments, CancellationToken ct)
    {
        // Directory.CreateTempSubdirectory gives a folder only the current user can open (per-user %TEMP% on Windows, 0700 elsewhere).
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
