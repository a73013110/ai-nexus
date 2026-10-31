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
        .Concat(Validators.Select(x => x.Problem)).OfType<DiagnosticInfo>();
}
