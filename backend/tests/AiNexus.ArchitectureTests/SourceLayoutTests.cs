
namespace AiNexus.ArchitectureTests;

/// <summary>
/// 型別靠檔名找到、檔案靠資料夾找到：每個檔案以它的主要型別命名，namespace 為專案根 namespace 加上資料夾路徑。
/// 輔助型別（use case 的 DTO 與驗證器、實體的設定類別）可以與主型別同檔；其他檔案也會用到的型別則要獨立成檔。
/// 測試依其涵蓋的 use case 所屬模組歸檔，讓修改單一模組時只需跑一個資料夾。
/// </summary>
public sealed class SourceLayoutTests
{
    public static TheoryData<string> Projects =>
        ["AiNexus.Host", "AiNexus.Features", "AiNexus.Data", "AiNexus.Platform", "AiNexus.UnitTests", "AiNexus.IntegrationTests", "AiNexus.ArchitectureTests"];

    public static TheoryData<string> TestProjects => ["AiNexus.UnitTests", "AiNexus.IntegrationTests", "AiNexus.ArchitectureTests"];

    public static TheoryData<string> ModuleTestProjects => ["AiNexus.UnitTests", "AiNexus.IntegrationTests"];

    [Theory]
    [MemberData(nameof(Projects))]
    public void EveryFileDeclaresTheTypeItIsNamedAfter(string project)
    {
        var mismatched = SourceTree.Files(project)
            .Where(path => !SourceTree.TopLevelTypes(path).Contains(Path.GetFileNameWithoutExtension(path)))
            .Select(path => $"{SourceTree.Relative(path)}: [{string.Join(", ", SourceTree.TopLevelTypes(path))}]").Order(StringComparer.Ordinal).ToList();
        Assert.True(mismatched.Count == 0, "Name the file after its main type, or move the other types to files of their own:\n" + string.Join("\n", mismatched));
    }

    [Theory]
    [MemberData(nameof(Projects))]
    public void NamespacesFollowFolders(string project)
    {
        var mismatched = SourceTree.Files(project).Select(path => (Path: SourceTree.Relative(path), Actual: SourceTree.Namespace(path)))
            .Select(x => (x.Path, x.Actual, Expected: string.Join('.', x.Path.Split('/')[..^1])))
            .Where(x => x.Actual != x.Expected).Select(x => $"{x.Path}: {x.Actual} (expected {x.Expected})").Order(StringComparer.Ordinal).ToList();
        Assert.True(mismatched.Count == 0, "Namespaces must match folders:\n" + string.Join("\n", mismatched));
    }

    /// <summary>
    /// 模組檔案數不超過 <see cref="MaxModuleRootFiles"/> 時維持扁平；超過就把 use case、實體與服務放進模組下一層的能力資料夾，
    /// 模組根目錄只保留跨整個模組的檔案。路徑為 <c>AiNexus.Features/&lt;Area&gt;/&lt;Module&gt;/[&lt;能力&gt;/]檔案</c>。
    /// </summary>
    [Fact]
    public void LargeModulesAreSplitIntoCapabilityFolders()
    {
        var paths = SourceTree.Files("AiNexus.Features").Select(SourceTree.Relative).Select(path => path.Split('/'))
            .Where(parts => parts.Length > 3).ToList();
        var crowded = paths.Where(parts => parts.Length == 4).GroupBy(parts => parts[1] + "/" + parts[2])
            .Where(module => module.Count() > MaxModuleRootFiles).Select(module => $"{module.Key}: {module.Count()} files at the root");
        var nested = paths.Where(parts => parts.Length > 5).Select(parts => string.Join('/', parts));
        var offending = crowded.Concat(nested).Order(StringComparer.Ordinal).ToList();
        Assert.True(offending.Count == 0, "Group a large module's files into capability folders, one level deep:\n" + string.Join("\n", offending));
    }

    private const int MaxModuleRootFiles = 15;

    /// <summary>
    /// 後端模組依區分組（ADR 0016）：Features 根目錄底下只有允許的區（PascalCase），區根目錄只放模組資料夾。
    /// 區不是依賴邊界、schema 也不帶區，所以模組名在所有區之間必須唯一。
    /// </summary>
    [Fact]
    public void BackendModulesAreGroupedIntoAreas()
    {
        var areas = SourceTree.Areas();
        Assert.NotEmpty(areas);
        var unknown = areas.Where(area => !SourceTree.AllowedAreas.Contains(area.ToLowerInvariant()) || area != Pascal(area.ToLowerInvariant()))
            .Select(area => $"{area}: not an area ({string.Join(", ", SourceTree.AllowedAreas.Select(Pascal))})");
        var files = areas.SelectMany(area => Directory.EnumerateFiles(Path.Combine(SourceTree.FeaturesRoot, area)))
            .Select(path => Path.GetRelativePath(SourceTree.FeaturesRoot, path).Replace('\\', '/') + ": files belong in a module folder");
        var duplicates = SourceTree.ModuleFolders().GroupBy(x => x.Module, StringComparer.Ordinal).Where(g => g.Count() > 1)
            .Select(g => $"{g.Key}: module name used in {string.Join(", ", g.Select(x => x.Area))}");
        var offending = unknown.Concat(files).Concat(duplicates).Order(StringComparer.Ordinal).ToList();
        Assert.True(offending.Count == 0, "Put each module in AiNexus.Features/<Area>/<Module>/ with a unique module name:\n" + string.Join("\n", offending));
    }

    /// <summary>前端與後端用同樣的區：features 底下只有允許的區（小寫），區根目錄只放模組資料夾。</summary>
    [Fact]
    public void FrontendFeaturesAreGroupedIntoAreas()
    {
        var root = FrontendFeatures();
        var areas = Directory.EnumerateDirectories(root).Select(Path.GetFileName).OfType<string>().ToList();
        Assert.NotEmpty(areas);
        var unknown = areas.Where(area => !SourceTree.AllowedAreas.Contains(area))
            .Select(area => $"{area}: not an area ({string.Join(", ", SourceTree.AllowedAreas)})");
        var files = areas.SelectMany(area => Directory.EnumerateFiles(Path.Combine(root, area)))
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/') + ": files belong in a module folder");
        var offending = unknown.Concat(files).Order(StringComparer.Ordinal).ToList();
        Assert.True(offending.Count == 0, "Put each frontend feature in features/<area>/<module-kebab>/:\n" + string.Join("\n", offending));
    }

    /// <summary>
    /// 前端 <c>features/&lt;area&gt;/&lt;module-kebab&gt;</c> 對應擁有其 API 的後端 <c>Features/&lt;Area&gt;/&lt;Module&gt;</c>，兩邊可以用名稱互相找到（ADR 0016）。
    /// 不要求每個後端模組都有前端資料夾；<c>admin/design</c> 是設計系統展示頁，沒有對應的後端模組，因此排除。
    /// </summary>
    [Fact]
    public void FrontendFeaturesAreNamedAfterBackendModules()
    {
        var modules = SourceTree.ModuleFolders().Select(x => x.Area + "/" + x.Module).ToHashSet(StringComparer.Ordinal);
        var root = FrontendFeatures();
        var folders = Directory.EnumerateDirectories(root).SelectMany(area => Directory.EnumerateDirectories(area))
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/')).Where(folder => folder != "admin/design").ToList();
        Assert.NotEmpty(folders);
        var unmatched = folders.Where(folder => !modules.Contains(string.Join('/', folder.Split('/').Select(Pascal))))
            .Order(StringComparer.Ordinal).ToList();
        Assert.True(unmatched.Count == 0, "Name frontend feature folders after the backend area and module in kebab-case:\n" + string.Join("\n", unmatched));
    }

    private static string FrontendFeatures() => Path.Combine(SourceTree.Root, "frontend", "src", "app", "features");

    /// <summary>kebab-case 轉 PascalCase：<c>access-control</c> → <c>AccessControl</c>、<c>work</c> → <c>Work</c>。</summary>
    private static string Pascal(string kebab) => string.Concat(kebab.Split('-').Select(part => char.ToUpperInvariant(part[0]) + part[1..]));

    [Theory]
    [MemberData(nameof(ModuleTestProjects))]
    public void TestsAreFiledByModule(string project)
    {
        var folders = SourceTree.Modules().Union(["Host", "Data", "Platform", "Support"]).ToHashSet(StringComparer.Ordinal);
        // Support 底下的共用輔助檔不需要 Tests.cs 結尾；其他檔案必須是模組或 Host/Platform 底下的測試。
        bool IsMisplaced(string[] parts)
        {
            if (parts.Length < 3 || !folders.Contains(parts[1]))
            {
                return true;
            }

            var isSupport = parts[1] == "Support";
            return !isSupport && !parts[^1].EndsWith("Tests.cs", StringComparison.Ordinal);
        }

        var misplaced = SourceTree.Files(project).Select(SourceTree.Relative).Select(path => path.Split('/'))
            .Where(IsMisplaced)
            .Select(parts => string.Join('/', parts)).Order(StringComparer.Ordinal).ToList();
        Assert.True(misplaced.Count == 0, "Put tests in <Module>/<UseCase>Tests.cs (or Host, Data, Platform) and shared helpers in Support:\n" + string.Join("\n", misplaced));
    }

    /// <summary>測試方法名稱以 PascalCase 句子描述行為；類別名稱已指出 use case，方法名不必重複。</summary>
    [Theory]
    [MemberData(nameof(TestProjects))]
    public void TestMethodsArePascalCaseSentences(string project)
    {
        var offending = SourceTree.Files(project).SelectMany(path => SourceTree.TestMethods(path).Select(name => (Path: SourceTree.Relative(path), Name: name)))
            .Where(x => !char.IsUpper(x.Name[0]) || x.Name.Contains('_', StringComparison.Ordinal) || x.Name.EndsWith("Async", StringComparison.Ordinal))
            .Select(x => $"{x.Path}: {x.Name}").Order(StringComparer.Ordinal).ToList();
        Assert.True(offending.Count == 0, "Name tests as PascalCase sentences without underscores or an Async suffix:\n" + string.Join("\n", offending));
        Assert.NotEmpty(SourceTree.Files(project).SelectMany(SourceTree.TestMethods));
    }
}
