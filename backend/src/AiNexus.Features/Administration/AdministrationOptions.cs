using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiNexus.Features.Administration;

public sealed class AdministrationOptions { public string[] BootstrapAdministrators { get; set; } = []; }

internal sealed class AdministrationOptionsValidator : IValidateOptions<AdministrationOptions>
{
    public ValidateOptionsResult Validate(string? name, AdministrationOptions x)
        => x.BootstrapAdministrators.Length <= 20 && x.BootstrapAdministrators.All(a => a.Length is > 0 and <= 64 && a.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.'))
            ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail("Invalid bootstrap administrator accounts.");
}
