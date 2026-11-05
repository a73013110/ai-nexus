using System.Net;
using System.Net.Http.Json;
using AiNexus.Features.Platform.Account;
using AiNexus.Features.Admin.Identity.Users;

namespace AiNexus.IntegrationTests.Account;

public sealed class UserSettingsTests
{
    [Fact]
    public async Task SettingsAreAccountScopedAndLegacyAppearanceUpdatesPreserveReadingPreferences()
    {
        await using var factory = new AppFactory(); using var alice = await factory.SignedInAsync(); using var bob = await factory.SignedInAsync("bob");
        var original = (await alice.GetFromJsonAsync<UserSettingsDto>("/api/v1/settings"))!;
        Assert.Equal(100, original.Appearance.TextScale); Assert.Equal(1.2, original.ReadingLineHeight); Assert.Equal(240, original.SidebarWidth);
        var modified = original with { Appearance = original.Appearance with { TextScale = 120 }, ReadingLineHeight = 1, EnterToSend = false, AutoFollow = false, SaveLocalDrafts = false, SidebarWidth = 320 };
        (await alice.PutAsJsonAsync("/api/v1/settings", modified)).EnsureSuccessStatusCode();
        (await alice.PutAsJsonAsync("/api/v1/preferences", new PreferencesDto("dark", true, null, 110))).EnsureSuccessStatusCode();
        var saved = (await alice.GetFromJsonAsync<UserSettingsDto>("/api/v1/settings"))!;
        Assert.Equal(1, saved.ReadingLineHeight); Assert.False(saved.EnterToSend); Assert.False(saved.AutoFollow); Assert.False(saved.SaveLocalDrafts);
        Assert.Equal(("dark", 110), (saved.Appearance.Theme, saved.Appearance.TextScale));
        Assert.Equal(100, (await bob.GetFromJsonAsync<UserSettingsDto>("/api/v1/settings"))!.Appearance.TextScale);
    }

    [Theory]
    [InlineData(90)]
    [InlineData(140)]
    public async Task TextScaleOutsideTheOfferedRangeIsRejectedOnBothEndpoints(int scale)
    {
        await using var factory = new AppFactory(); using var alice = await factory.SignedInAsync();
        var original = (await alice.GetFromJsonAsync<UserSettingsDto>("/api/v1/settings"))!;
        var preferences = await alice.PutAsJsonAsync("/api/v1/preferences", original.Appearance with { TextScale = scale });
        Assert.Equal(HttpStatusCode.BadRequest, preferences.StatusCode);
        Assert.Contains("invalid_appearance", await preferences.Content.ReadAsStringAsync());
        var settings = await alice.PutAsJsonAsync("/api/v1/settings", original with { Appearance = original.Appearance with { TextScale = scale } });
        Assert.Equal(HttpStatusCode.BadRequest, settings.StatusCode);
        Assert.Equal(original, await alice.GetFromJsonAsync<UserSettingsDto>("/api/v1/settings"));
    }

    [Fact]
    public async Task InvalidSettingsDoNotPartiallySaveAppearanceAndMutationsRequireCsrf()
    {
        await using var factory = new AppFactory(); using var alice = await factory.SignedInAsync();
        var original = (await alice.GetFromJsonAsync<UserSettingsDto>("/api/v1/settings"))!;
        Assert.Equal(HttpStatusCode.BadRequest, (await alice.PutAsJsonAsync("/api/v1/settings", original with { SidebarWidth = 100, Appearance = new("dark", false, null, 100) })).StatusCode);
        Assert.Equal(original, await alice.GetFromJsonAsync<UserSettingsDto>("/api/v1/settings"));
        alice.DefaultRequestHeaders.Remove("X-CSRF-Token");
        Assert.Equal(HttpStatusCode.Forbidden, (await alice.PutAsJsonAsync("/api/v1/settings", original)).StatusCode);
    }
}
