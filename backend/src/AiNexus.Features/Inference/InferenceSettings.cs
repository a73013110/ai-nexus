namespace AiNexus.Features.Inference;

/// <summary>Reads the deployment settings (<c>Inference</c>, <c>Prompts</c>) into <see cref="InferenceOptions"/>; the settings layout is separate from the options services use.</summary>
public static class InferenceSettings
{
    public static void Bind(IConfiguration config, InferenceOptions options)
    {
        var section = config.GetSection("Inference");
        section.GetSection("Execution").Bind(options);
        section.GetSection("ModelPolicy").Bind(options);
        var providers = section.GetSection("Providers");
        options.BaseUrl = providers["Ollama:Endpoint"] ?? options.BaseUrl;
        options.GoogleApiKey = providers["Google:ApiKey"] ?? "";
        options.SystemPrompt = config["Prompts:DefaultSystemInstruction"] ?? options.SystemPrompt;
        options.DefaultModelId = section["ModelPolicy:DefaultModelId"];
        options.Models = [];
        options.ProviderConcurrency = new(StringComparer.Ordinal);
        foreach (var provider in providers.GetChildren().Where(x => x.GetValue<bool>("Enabled")))
        {
            var id = provider.Key.ToLowerInvariant();
            options.ProviderConcurrency.Add(id, provider.GetValue("MaxConcurrency", 1));
            foreach (var modelSection in provider.GetSection("Models").GetChildren())
            {
                var model = modelSection.Get<ModelProfile>()!;
                model.ImageCapabilityOverride = modelSection.GetValue<bool?>("SupportsImages");
                model.Provider = id;
                model.ProviderModelId = model.Id;
                model.Id = id + "/" + model.ProviderModelId;
                options.Models.Add(model);
            }
        }
    }
}
