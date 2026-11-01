namespace AiNexus.Tests;

/// <summary>
/// 儲存庫根目錄（有 global.json 的那層），由建置輸出往上找：不寫死層數，換 <c>--artifacts-path</c> 或輸出結構時一樣找得到。
/// 這個檔由 tests/Directory.Build.props 連結進每個測試專案，不要在專案裡再寫一份。
/// </summary>
internal static class Repository
{
    public static readonly string Root = Find();

    private static string Find()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "global.json"))) return directory.FullName;
        throw new InvalidOperationException("Repository root (global.json) was not found.");
    }
}
