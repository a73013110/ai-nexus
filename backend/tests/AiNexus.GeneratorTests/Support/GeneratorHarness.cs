using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AiNexus.GeneratorTests.Support;

/// <summary>把範例原始碼編譯成記憶體中的組件並執行產生器；引用的是測試程序本身載入的組件（含 ASP.NET Core 與 Platform）。</summary>
internal static class GeneratorHarness
{
    public static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Latest);

    private static readonly Lazy<MetadataReference[]> References = new(() => [.. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path))]);

    public static CSharpCompilation Compile(params string[] sources) => CSharpCompilation.Create("Sample",
        sources.Select((source, index) => CSharpSyntaxTree.ParseText(source, ParseOptions, path: $"Source{index}.cs")), References.Value,
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

    /// <summary>開啟步驟追蹤，測試才能讀到每一步是重算還是沿用快取。</summary>
    public static GeneratorDriver Driver(IIncrementalGenerator generator) => CSharpGeneratorDriver.Create([generator.AsSourceGenerator()],
        parseOptions: ParseOptions, driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

    /// <summary>執行一次產生器，回傳產生器的診斷、產生的原始碼，以及加入產出後的編譯錯誤。</summary>
    public static GeneratorRun Run(IIncrementalGenerator generator, params string[] sources)
    {
        var driver = Driver(generator).RunGeneratorsAndUpdateCompilation(Compile(sources), out var output, out _);
        var result = driver.GetRunResult();
        var errors = output.GetDiagnostics().Where(x => x.Severity == DiagnosticSeverity.Error).ToList();
        return new(result.Diagnostics, string.Concat(result.GeneratedTrees.Select(tree => tree.ToString())), errors);
    }
}
