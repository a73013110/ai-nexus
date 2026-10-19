using System.Reflection;
using System.Text.RegularExpressions;
using AiNexus.Features.Admin.AccessControl;

namespace AiNexus.UnitTests.AccessControl;

/// <summary>目錄是頁面、授權與網址的唯一來源；這裡檢查宣告本身的規則（ADR 0016），與 Seed、Angular 路由的比對另有測試。</summary>
public sealed class FeatureCatalogTests
{
    private static readonly Regex IdFormat = new("^[a-z]+(-[a-z]+)*(\\.[a-z]+(-[a-z]+)*)*$", RegexOptions.CultureInvariant);

    [Fact]
    public void IdsAreLowercaseKebabSegmentsSeparatedByDots()
    {
        var invalid = FeatureCatalog.All.Select(x => x.Id).Concat(FeatureCatalog.Modules.Select(x => x.Id)).Where(id => !IdFormat.IsMatch(id)).ToList();
        Assert.True(invalid.Count == 0, "ID 格式應為 <module>.<page>[.<action>]，每段小寫 kebab-case：" + string.Join(", ", invalid));
    }

    [Fact]
    public void FirstSegmentOfEveryIdIsItsModule()
    {
        var wrong = FeatureCatalog.All.Where(x => x.Id.Split('.')[0] != x.Module.Id).Select(x => $"{x.Id} → {x.Module.Id}").ToList();
        Assert.True(wrong.Count == 0, "功能 ID 的第一段必須是所屬模組：" + string.Join(", ", wrong));
    }

    [Fact]
    public void ModulesAndAreasAreDeclaredOnceAndUsed()
    {
        Assert.Equal(FeatureCatalog.Modules.Count, FeatureCatalog.Modules.Select(x => x.Id).Distinct().Count());
        Assert.Equal(FeatureCatalog.Areas.Count, FeatureCatalog.Areas.Select(x => x.Id).Distinct().Count());
        Assert.All(FeatureCatalog.All, x => Assert.Contains(x.Module, FeatureCatalog.Modules));
        Assert.All(FeatureCatalog.Modules, x => Assert.Contains(x.Area, FeatureCatalog.Areas));
        Assert.All(FeatureCatalog.Modules, x => Assert.Contains(FeatureCatalog.All, feature => feature.Module == x));
    }

    [Fact]
    public void ActionsBelongToAPageOfTheSameModule()
    {
        var orphans = FeatureCatalog.All.Where(x => x.Kind == FeatureKind.Action)
            .Where(x => FeatureCatalog.Find(x.Id[..x.Id.LastIndexOf('.')]) is not { Kind: FeatureKind.Page } parent || parent.Module != x.Module)
            .Select(x => x.Id).ToList();
        Assert.True(orphans.Count == 0, "動作的上層（去掉最後一段）必須是同模組的頁面：" + string.Join(", ", orphans));
    }

    [Fact]
    public void RoutesAreDerivedFromAreaAndIdAndAreUnique()
    {
        foreach (var feature in FeatureCatalog.All)
        {
            var expected = feature.Kind == FeatureKind.Action ? null : "/" + feature.Module.Area.Id + "/" + string.Join('/', feature.Id.Split('.'));
            Assert.Equal(expected, feature.Route);
        }
        var routes = FeatureCatalog.All.Select(x => x.Route).OfType<string>().ToList();
        Assert.Equal(routes.Count, routes.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal("/work/sharing/reader", FeatureCatalog.RouteOf(FeatureCatalog.SharingReader));
        Assert.Equal("/admin/diagnostics/logs", FeatureCatalog.RouteOf(FeatureCatalog.DiagnosticsLogs));
    }

    [Fact]
    public void ConstantsAndDefinitionsMatchOneToOne()
    {
        var constants = typeof(FeatureCatalog).GetFields(BindingFlags.Public | BindingFlags.Static).Where(x => x.IsLiteral)
            .Select(x => (string)x.GetRawConstantValue()!).ToList();
        var ids = FeatureCatalog.All.Select(x => x.Id).ToList();
        Assert.Equal(constants.Count, constants.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Empty(constants.Except(ids, StringComparer.Ordinal));
        Assert.Empty(ids.Except(constants, StringComparer.Ordinal));
    }

    [Fact]
    public void GrantsArePagesAndActionsOnly()
    {
        Assert.All(FeatureCatalog.Grants, x => Assert.NotEqual(FeatureKind.Reader, x.Kind));
        Assert.Equal(FeatureCatalog.All.Count(x => x.Kind != FeatureKind.Reader), FeatureCatalog.Grants.Count);
        Assert.Throws<ArgumentException>(() => FeatureCatalog.RouteOf(FeatureCatalog.DiagnosticsLogsExport));
        Assert.Throws<ArgumentException>(() => FeatureCatalog.RouteOf("retired"));
        Assert.Null(FeatureCatalog.Find("retired"));
    }
}
