using System.Reflection;
using AiNexus.Platform.Diagnostics;
using AiNexus.Platform.Errors;

namespace AiNexus.ArchitectureTests;

/// <summary>
/// 前端只對 4xx 顯示核准提示（使用者能自己修正），5xx 一律顯示查證代碼；4xx 代碼沒有提示時，使用者只看到通用訊息、不知道該改什麼。
/// 以反射收集所有宣告成靜態欄位的 <see cref="Error"/>（<c>&lt;Module&gt;Errors</c>、<c>RequestValidation.Failed</c> 等），新增代碼漏了提示在這裡失敗。
/// </summary>
public sealed class ErrorCatalogTests
{
    [Fact]
    public void EveryClientErrorHasAReviewedHint()
    {
        var errors = new[] { Assemblies.Platform, Assemblies.Data, Assemblies.Features, Assemblies.Host }
            .SelectMany(assembly => assembly.GetTypes()).Where(type => !type.ContainsGenericParameters)
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(field => field.FieldType == typeof(Error))
            .Select(field => (Field: field.DeclaringType!.Name + "." + field.Name, Error: (Error)field.GetValue(null)!))
            .ToList();
        Assert.NotEmpty(errors);
        var missing = errors.Where(x => Problems.Status(x.Error.Kind) < 500 && !PublicErrorCatalog.Contains(x.Error.Code))
            .Select(x => $"{x.Field}: {x.Error.Code}").Order(StringComparer.Ordinal).ToList();
        Assert.True(missing.Count == 0, "Client errors need a reviewed hint in PublicErrorCatalog:\n" + string.Join('\n', missing));
    }
}
