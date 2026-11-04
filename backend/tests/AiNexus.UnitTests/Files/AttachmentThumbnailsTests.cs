using AiNexus.Features.Work.Files.Thumbnails;
using SkiaSharp;

namespace AiNexus.UnitTests.Files;

public sealed class AttachmentThumbnailsTests
{
    [Fact]
    public void LargeImagesShrinkToTheLongEdgeAsWebp()
    {
        using var thumbnail = Decode(AttachmentThumbnails.Encode(Image(600, 300, SKEncodedImageFormat.Png))!);
        Assert.Equal((AttachmentThumbnails.MaxEdge, AttachmentThumbnails.MaxEdge / 2), (thumbnail.Width, thumbnail.Height));
    }

    [Fact]
    public void SmallImagesAreNotEnlarged()
    {
        using var thumbnail = Decode(AttachmentThumbnails.Encode(Image(40, 20, SKEncodedImageFormat.Jpeg))!);
        Assert.Equal((40, 20), (thumbnail.Width, thumbnail.Height));
    }

    [Fact]
    public void UnreadableBytesHaveNoThumbnail() => Assert.Null(AttachmentThumbnails.Encode("not an image"u8.ToArray()));

    [Fact]
    public void ExifOrientationIsAppliedSoPortraitPhotosStandUp()
    {
        // 左半紅、右半藍的橫圖，EXIF 標記「順時針轉 90 度顯示」：轉正後是直圖，原本的左邊（紅）在上面。
        var jpeg = WithOrientation(Image(80, 40, SKEncodedImageFormat.Jpeg, split: true), 6);
        using var thumbnail = Decode(AttachmentThumbnails.Encode(jpeg)!);
        Assert.Equal((40, 80), (thumbnail.Width, thumbnail.Height));
        Assert.True(thumbnail.GetPixel(20, 10).Red > 200);
        Assert.True(thumbnail.GetPixel(20, 70).Blue > 200);
    }

    private static byte[] Image(int width, int height, SKEncodedImageFormat format, bool split = false)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        surface.Canvas.Clear(SKColors.Red);
        if (split)
        {
            using var paint = new SKPaint { Color = SKColors.Blue };
            surface.Canvas.DrawRect(width / 2f, 0, width / 2f, height, paint);
        }
        using var image = surface.Snapshot();
        return image.Encode(format, 95).ToArray();
    }

    private static SKBitmap Decode(byte[] webp)
    {
        Assert.Equal("RIFF"u8.ToArray(), webp[..4]);
        return SKBitmap.Decode(webp);
    }

    // 在 SOI 之後插入只有方向欄位的 APP1（Exif、big-endian TIFF、一個 IFD 項目）。
    private static byte[] WithOrientation(byte[] jpeg, byte orientation)
    {
        byte[] exif =
        [
            0xFF, 0xE1, 0x00, 0x22, (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0, 0,
            (byte)'M', (byte)'M', 0x00, 0x2A, 0x00, 0x00, 0x00, 0x08,
            0x00, 0x01, 0x01, 0x12, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01, 0x00, orientation, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
        ];
        return [.. jpeg[..2], .. exif, .. jpeg[2..]];
    }
}
