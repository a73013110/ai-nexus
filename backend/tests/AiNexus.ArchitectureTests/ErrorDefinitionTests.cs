using System.Reflection;
using System.Text.RegularExpressions;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Errors;

namespace AiNexus.ArchitectureTests;

/// <summary>
/// 錯誤代碼是公開合約，只在一處宣告：Features 放 <c>&lt;Module&gt;Errors</c>，Platform／Data／Host 放擁有它的型別（如 <c>TableQuery.Invalid</c>），一律是靜態欄位。
/// 這樣反射看得到每一個代碼，提示檢查才完整；行內 <c>Error.Upstream("x")</c> 或以字串參數組出的錯誤都看不到。
/// 只寫日誌的代碼（<c>Issues.Report</c>）也是 <see cref="Error"/>，與回應、稽核共用同一套。
/// </summary>
public sealed partial class ErrorDefinitionTests
{
    private static readonly string[] Projects = ["AiNexus.Platform", "AiNexus.Data", "AiNexus.Features", "AiNexus.Host"];

    [Fact]
    public void ErrorsAreDeclaredOnlyAsStaticFields()
    {
        var misplaced = Projects.SelectMany(SourceTree.Files)
            .Where(path => SourceTree.Relative(path) != "AiNexus.Platform/Errors/Error.cs")
            .SelectMany(path => File.ReadLines(path).Select((line, index) => (Path: SourceTree.Relative(path), Line: index + 1, Text: line)))
            .Where(x => Factory().IsMatch(x.Text) && !(Field().IsMatch(x.Text) && (!x.Path.StartsWith("AiNexus.Features/", StringComparison.Ordinal) || x.Path.EndsWith("Errors.cs", StringComparison.Ordinal))))
            .Select(x => $"{x.Path}:{x.Line}").ToList();
        Assert.True(misplaced.Count == 0, "Declare errors as static readonly fields (<Module>Errors in Features, the owning type elsewhere):\n" + string.Join('\n', misplaced));
    }

    // 前端只對 4xx 顯示核准提示（使用者能自己修正），5xx 一律顯示查證代碼；4xx 代碼沒有提示時，使用者只看到通用訊息、不知道該改什麼。
    [Fact]
    public void EveryClientErrorHasAReviewedHint()
    {
        var missing = Declared().Where(x => Problems.Status(x.Error.Kind) < 500 && !PublicErrorCatalog.Contains(x.Error.Code))
            .Select(x => $"{x.Field}: {x.Error.Code}").Order(StringComparer.Ordinal).ToList();
        Assert.True(missing.Count == 0, "Client errors need a reviewed hint in PublicErrorCatalog:\n" + string.Join('\n', missing));
    }

    // 通用代碼只有 HttpErrors 一份；模組再宣告一份同代碼的，分類或提示改了就會分岔。意思更精確時另取專屬代碼。
    [Fact]
    public void GenericErrorsAreNotRedeclared()
    {
        var generic = typeof(HttpErrors).GetFields().Select(field => ((Error)field.GetValue(null)!).Code).ToHashSet(StringComparer.Ordinal);
        var redeclared = Declared().Where(x => !x.Field.StartsWith(nameof(HttpErrors) + ".", StringComparison.Ordinal) && generic.Contains(x.Error.Code))
            .Select(x => $"{x.Field}: {x.Error.Code}").ToList();
        Assert.True(redeclared.Count == 0, "Use HttpErrors instead of redeclaring a generic code:\n" + string.Join('\n', redeclared));
    }

    private static List<(string Field, Error Error)> Declared()
    {
        var errors = new[] { Assemblies.Platform, Assemblies.Data, Assemblies.Features, Assemblies.Host }
            .SelectMany(assembly => assembly.GetTypes()).Where(type => !type.ContainsGenericParameters)
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(field => field.FieldType == typeof(Error))
            .Select(field => (Field: field.DeclaringType!.Name + "." + field.Name, Error: (Error)field.GetValue(null)!))
            .ToList();
        Assert.NotEmpty(errors);
        return errors;
    }

    [GeneratedRegex(@"\bError\.(?:Invalid|Unauthenticated|Forbidden|NotFound|Conflict|TooLarge|Unprocessable|RateLimited|Upstream|Unavailable|Timeout)\(")]
    private static partial Regex Factory();

    [GeneratedRegex(@"^\s*(?:public|internal|private)\s+static\s+readonly\s+Error\s+\w+\s*=\s*Error\.\w+\(""[a-z0-9_]+""\);")]
    private static partial Regex Field();
}
