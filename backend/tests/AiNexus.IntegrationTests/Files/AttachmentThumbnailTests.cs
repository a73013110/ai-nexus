using System.Net;
using SkiaSharp;
using static AiNexus.IntegrationTests.Support.AttachmentApi;

namespace AiNexus.IntegrationTests.Files;

public sealed class AttachmentThumbnailTests
{
    [Fact]
    public async Task OwnersGetASmallWebpWhileOthersAndDocumentsGetNotFound()
    {
        await using var factory = new AppFactory();
        using var alice = await factory.SignedInAsync();
        using var bob = await factory.SignedInAsync("bob");
        var photo = await Upload(alice, "photo.png", Png(1200, 900));
        var notes = await Upload(alice, "notes.txt", "plain text");

        using var response = await alice.GetAsync($"/api/v1/attachments/{photo.Id}/thumbnail");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/webp", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
        using var thumbnail = SKBitmap.Decode(await response.Content.ReadAsByteArrayAsync());
        Assert.Equal((256, 192), (thumbnail.Width, thumbnail.Height));

        Assert.Equal(HttpStatusCode.NotFound, (await alice.GetAsync($"/api/v1/attachments/{notes.Id}/thumbnail")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/v1/attachments/{photo.Id}/thumbnail")).StatusCode);
    }

    private static byte[] Png(int width, int height)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        surface.Canvas.Clear(SKColors.SteelBlue);
        using var image = surface.Snapshot();
        return image.Encode(SKEncodedImageFormat.Png, 100).ToArray();
    }
}
