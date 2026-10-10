using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Account;
using AiNexus.Features.Identity.Users;

namespace AiNexus.IntegrationTests.Account;

public sealed class UserSettingsTests
{
    [Fact]
    public async Task SettingsAreAccountScopedAndLegacyAppearanceUpdatesPreserveReadingPreferences()
    {
        await using var factory = new NexusFactory(); using var alice = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        var original = (await alice.GetFromJsonAsync<UserSettingsDto>("/api/v1/settings"))!;
        Assert.Equal(15, original.ReadingFontSize); Assert.Equal(1.2, original.ReadingLineHeight); Assert.Equal(240, original.SidebarWidth);
        var modified = original with { ReadingFontSize = 12, ReadingLineHeight = 1, EnterToSend = false, AutoFollow = false, SaveLocalDrafts = false, SidebarWidth = 320 };
        (await alice.PutAsJsonAsync("/api/v1/settings", modified)).EnsureSuccessStatusCode();
        (await alice.PutAsJsonAsync("/api/v1/preferences", new PreferencesDto("dark", true, null))).EnsureSuccessStatusCode();
        var saved = (await alice.GetFromJsonAsync<UserSettingsDto>("/api/v1/settings"))!;
        Assert.Equal(12, saved.ReadingFontSize); Assert.Equal(1, saved.ReadingLineHeight); Assert.False(saved.EnterToSend); Assert.False(saved.AutoFollow); Assert.False(saved.SaveLocalDrafts);
        Assert.Equal("dark", saved.Appearance.Theme);
        Assert.Equal(15, (await bob.GetFromJsonAsync<UserSettingsDto>("/api/v1/settings"))!.ReadingFontSize);
    }

    [Fact]
    public async Task InvalidSettingsDoNotPartiallySaveAppearanceAndMutationsRequireCsrf()
    {
        await using var factory = new NexusFactory(); using var alice = await factory.SignedInAsync();
        var original = (await alice.GetFromJsonAsync<UserSettingsDto>("/api/v1/settings"))!;
        Assert.Equal(HttpStatusCode.BadRequest, (await alice.PutAsJsonAsync("/api/v1/settings", original with { ReadingFontSize = 8, Appearance = new("dark", false, null) })).StatusCode);
        Assert.Equal(original, await alice.GetFromJsonAsync<UserSettingsDto>("/api/v1/settings"));
        alice.DefaultRequestHeaders.Remove("X-Nexus-CSRF");
        Assert.Equal(HttpStatusCode.Forbidden, (await alice.PutAsJsonAsync("/api/v1/settings", original)).StatusCode);
    }
}
