using AiNexus.Generators.Common;

namespace AiNexus.Generators.Composition;

/// <summary>組件內要組合的全部型別，各自依完整名稱排序；內容不變時不重新產生。</summary>
internal sealed record FeatureComposition
{
    public FeatureComposition(EquatableArray<ModuleRegistration> modules, EquatableArray<SliceRegistration> slices, EquatableArray<ValidatorRegistration> validators)
    {
        Modules = modules;
        Slices = slices;
        Validators = validators;
    }

    public EquatableArray<ModuleRegistration> Modules { get; }

    public EquatableArray<SliceRegistration> Slices { get; }

    public EquatableArray<ValidatorRegistration> Validators { get; }

    public IEnumerable<DiagnosticInfo> Problems => Modules.Select(x => x.Problem).Concat(Slices.Select(x => x.Problem))
        .Concat(Validators.Select(x => x.Problem)).OfType<DiagnosticInfo>().Concat(DuplicateValidators());

    // 驗證器已依名稱排序：同一 request 型別的第一個之外都回報，指向多出來的那一個。
    private IEnumerable<DiagnosticInfo> DuplicateValidators() => Validators.GroupBy(x => x.RequestType, StringComparer.Ordinal).Where(g => g.Count() > 1)
        .SelectMany(g => g.Skip(1).Select(x => new DiagnosticInfo(CompositionDiagnostics.DuplicateValidator, x.Location, x.Display, x.RequestType, g.First().Display)));
}
