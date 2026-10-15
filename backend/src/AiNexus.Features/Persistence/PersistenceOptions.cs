using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Persistence;

/// <summary>How the host treats the schema of the Nexus database; its connection is <c>Databases:Nexus</c>.</summary>
public sealed class PersistenceOptions : IValidatableObject
{
    public const string Section = "Persistence";

    /// <summary>Applies pending migrations at startup. When false, startup only checks them and stops if any are pending.</summary>
    public bool ApplyMigrationsOnStartup { get; set; }

    /// <summary>
    /// A single flag has no rule of its own. Implementing this keeps the section on a generated validator like every
    /// other one (the generator refuses a type with nothing to validate); unknown keys still fail binding.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) => [];
}

[OptionsValidator]
public sealed partial class PersistenceOptionsValidator : IValidateOptions<PersistenceOptions>;
