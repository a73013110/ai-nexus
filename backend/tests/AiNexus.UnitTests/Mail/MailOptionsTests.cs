using AiNexus.Features.Admin.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AiNexus.UnitTests.Mail;

public sealed class MailOptionsTests
{
    private static MailOptions Resolve(string environment, Dictionary<string, string?> values)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateEmptyApplicationBuilder(new() { EnvironmentName = environment, ContentRootPath = Path.GetTempPath() });
        builder.Configuration.AddInMemoryCollection(values);
        MailModule.AddServices(builder);
        using var services = builder.Services.BuildServiceProvider();
        return services.GetRequiredService<IOptions<MailOptions>>().Value;
    }

    [Fact]
    public void DevelopmentWritesPickupFilesUnderTheWorkspaceWithoutAnySettings()
    {
        var root = Path.Combine(Path.GetTempPath(), "ainexus-workspace");
        var options = Resolve("Development", new() { ["LocalWorkspaceRoot"] = root });
        Assert.Equal(MailTransports.Pickup, options.Transport);
        Assert.Equal(Path.Combine(root, ".local", "mail"), options.Pickup.Directory);
        Assert.True(options.Enabled);
        Assert.Equal("ainexus.localhost", options.MessageIdHost);
    }

    [Fact]
    public void TestingDefaultsToDisabled() => Assert.Equal(MailTransports.Disabled, Resolve("Testing", []).Transport);

    [Fact]
    public void ProductionRequiresAnExplicitTransportAndTheSenderLinksAndHostOfSmtp()
    {
        var missing = Assert.Throws<OptionsValidationException>(() => Resolve("Production", []));
        Assert.Contains("Transport is required outside Development", missing.Message, StringComparison.Ordinal);

        var smtp = Assert.Throws<OptionsValidationException>(() => Resolve("Production", new() { ["Mail:Transport"] = "Smtp" }));
        Assert.Contains("From.Address", smtp.Message, StringComparison.Ordinal);
        Assert.Contains("PublicBaseUrl", smtp.Message, StringComparison.Ordinal);
        Assert.Contains("Smtp.Host", smtp.Message, StringComparison.Ordinal);

        Assert.Equal(MailTransports.Disabled, Resolve("Production", new() { ["Mail:Transport"] = "Disabled" }).Transport);
        var configured = Resolve("Production", new()
        {
            ["Mail:Transport"] = "Smtp", ["Mail:Smtp:Host"] = "smtp-relay.gmail.com", ["Mail:From:Address"] = "no-reply@company.com",
            ["Mail:PublicBaseUrl"] = "https://ainexus.company.com", ["Mail:Smtp:LocalDomain"] = "company.com",
        });
        Assert.Equal(("smtp-relay.gmail.com", 587, MailSmtpOptions.StartTls), (configured.Smtp.Host, configured.Smtp.Port, configured.Smtp.Security));
    }

    [Theory]
    [InlineData("Mail:Smtp:Security", "None", "only allowed for a loopback host")]
    [InlineData("Mail:PublicBaseUrl", "https://ainexus.company.com/app", "PublicBaseUrl")]
    [InlineData("Mail:AllowedRecipientDomains:0", "Company.com", "AllowedRecipientDomains")]
    [InlineData("Mail:Retention:ContentDays", "400", "ContentDays <= RecipientDays")]
    [InlineData("Mail:Smtp:ServerCertificateThumbprint", "not-hex", "ServerCertificateThumbprint")]
    public void InvalidSmtpSettingsFailAndNameTheKey(string key, string value, string message)
    {
        var values = new Dictionary<string, string?>
        {
            ["Mail:Transport"] = "Smtp", ["Mail:Smtp:Host"] = "mail.company.com", ["Mail:From:Address"] = "no-reply@company.com",
            ["Mail:PublicBaseUrl"] = "https://ainexus.company.com", [key] = value,
        };
        var error = Assert.Throws<OptionsValidationException>(() => Resolve("Production", values));
        Assert.Contains(message, error.Message, StringComparison.Ordinal);
    }
}
