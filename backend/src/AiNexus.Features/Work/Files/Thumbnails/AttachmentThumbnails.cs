using AiNexus.Data.App.Files;
using AiNexus.Platform.Errors;
using AiNexus.Platform.Modules;
using Microsoft.Extensions.Caching.Memory;
using SkiaSharp;

namespace AiNexus.Features.Work.Files.Thumbnails;

/// <summary>
/// 圖片附件的縮圖（WebP，長邊 <see cref="MaxEdge"/>）：清單與訊息只載入幾 KB，不再為了 72px 的方塊下載整張原圖。
/// 附件上傳後內容不變，所以結果依附件 ID 留在記憶體；授權仍由呼叫端（擁有者、分享、稽核檢視）各自檢查。
/// </summary>
[Singleton]
public sealed class AttachmentThumbnails : IDisposable
{
    /// <summary>長邊像素：訊息方塊 72px、草稿 48px 在 3x 螢幕仍清晰。</summary>
    public const int MaxEdge = 256;
    public const string ContentType = "image/webp";

    // 原檔上限 8 MB，但壓縮圖可以宣告極大的尺寸；解碼前先擋，避免一張圖吃掉數 GB 記憶體。
    private const long MaxSourcePixels = 50_000_000;
    private const int Quality = 82;

    // 以位元組計的預算：256px WebP 約 5–20 KB，32 MB 可放上千張，常用對話的縮圖都留得住。
    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 32 * 1024 * 1024 });

    /// <summary>不是圖片、或圖片無法解碼時回 <see cref="FilesErrors.NotFound"/>：前端改顯示檔案圖示。</summary>
    public async Task<Result<byte[]>> RenderAsync(Attachment file, Func<CancellationToken, Task<Stream>> open, CancellationToken ct)
    {
        if (!file.ContentType.StartsWith("image/", StringComparison.Ordinal)) return FilesErrors.NotFound;
        if (cache.TryGetValue<byte[]>(file.Id, out var cached)) return cached!;
        byte[] source;
        await using (var stream = await open(ct))
        {
            using var buffer = new MemoryStream(capacity: (int)file.Size);
            await stream.CopyToAsync(buffer, ct);
            source = buffer.ToArray();
        }
        var thumbnail = Encode(source);
        if (thumbnail is null) return FilesErrors.NotFound;
        cache.Set(file.Id, thumbnail, new MemoryCacheEntryOptions { Size = thumbnail.Length, SlidingExpiration = TimeSpan.FromHours(6) });
        return thumbnail;
    }

    /// <summary>把原圖轉成轉正、等比縮小的 WebP；無法解碼或尺寸超過上限時回 null。</summary>
    public static byte[]? Encode(byte[] source)
    {
        using var data = SKData.CreateCopy(source);
        using var codec = SKCodec.Create(data);
        if (codec is null || (long)codec.Info.Width * codec.Info.Height > MaxSourcePixels) return null;
        // JPEG 可以直接以較小的倍率解碼，省下大半的解碼時間與記憶體；其他格式回傳原尺寸。
        var target = Fit(codec.Info.Width, codec.Info.Height, MaxEdge);
        var decodeSize = codec.GetScaledDimensions(Math.Max((float)target.Width / codec.Info.Width, (float)target.Height / codec.Info.Height));
        using var decoded = new SKBitmap(new SKImageInfo(decodeSize.Width, decodeSize.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        var result = codec.GetPixels(decoded.Info, decoded.GetPixels());
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput)) return null;
        using var resized = decoded.Resize(new SKImageInfo(target.Width, target.Height, SKColorType.Rgba8888, SKAlphaType.Premul), new SKSamplingOptions(SKCubicResampler.Mitchell));
        if (resized is null) return null;
        using var image = Orient(resized, codec.EncodedOrigin);
        using var encoded = image.Encode(SKEncodedImageFormat.Webp, Quality);
        return encoded?.ToArray();
    }

    private static SKSizeI Fit(int width, int height, int edge)
    {
        var scale = Math.Min(1f, (float)edge / Math.Max(width, height));
        return new(Math.Max(1, (int)MathF.Round(width * scale)), Math.Max(1, (int)MathF.Round(height * scale)));
    }

    // 手機照片多半以 EXIF 方向旗標表示直拍；縮圖要轉正，否則與閱讀器裡的原圖方向不同。
    // 矩陣把原圖座標 (x, y) 對應到轉正後的位置，數值依 EXIF 方向 2–8 的定義。
    private static SKImage Orient(SKBitmap bitmap, SKEncodedOrigin origin)
    {
        float w = bitmap.Width, h = bitmap.Height;
        SKMatrix? matrix = origin switch
        {
            SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),
            SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1),
            SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),
            _ => null,
        };
        var image = SKImage.FromBitmap(bitmap);
        if (matrix is null) return image;
        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        using var surface = SKSurface.Create(new SKImageInfo(swap ? bitmap.Height : bitmap.Width, swap ? bitmap.Width : bitmap.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        surface.Canvas.SetMatrix(matrix.Value);
        // 旋轉角度都是 90 的倍數，像素一對一，不需要插值。
        using (image) surface.Canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
        return surface.Snapshot();
    }

    public void Dispose() => cache.Dispose();
}
