namespace base64reducer;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

public enum TargetFormat
{
    Jpeg,
    WebP
}

public static class ImageBase64Optimizer
{
    private readonly record struct EncodeResult(byte[] BinaryData, int Base64Length);

    public static byte[] OptimizeToBytes(
        Stream inputStream,
        int? maxBase64Length = null,
        int? maxBinarySizeBytes = null,
        TargetFormat targetFormat = TargetFormat.WebP,
        int maxSize = 0,
        int initialQuality = 90,
        int minQuality = 30)
    {
        var (binary, _) = OptimizeCore(inputStream, maxBase64Length, maxBinarySizeBytes, targetFormat, maxSize, initialQuality, minQuality);
        return binary;
    }

    public static string OptimizeToBase64(
        Stream inputStream,
        int? maxBase64Length = null,
        int? maxBinarySizeBytes = null,
        TargetFormat targetFormat = TargetFormat.WebP,
        int maxSize = 0,
        int initialQuality = 90,
        int minQuality = 30)
    {
        var (binary, base64Length) = OptimizeCore(inputStream, maxBase64Length, maxBinarySizeBytes, targetFormat, maxSize, initialQuality, minQuality);
        return Convert.ToBase64String(binary);
    }

    public static string OptimizeToDataUri(
        Stream inputStream,
        int? maxBase64Length = null,
        int? maxBinarySizeBytes = null,
        TargetFormat targetFormat = TargetFormat.WebP,
        int maxSize = 0,
        int initialQuality = 90,
        int minQuality = 30)
    {
        var (binary, base64Length) = OptimizeCore(inputStream, maxBase64Length, maxBinarySizeBytes, targetFormat, maxSize, initialQuality, minQuality);
        string mimeType = targetFormat switch
        {
            TargetFormat.Jpeg => "image/jpeg",
            TargetFormat.WebP => "image/webp",
            _ => "application/octet-stream"
        };
        return $"{mimeType};base64,{Convert.ToBase64String(binary)}";
    }

    public static byte[] OptimizeToBytes(
        string inputPath,
        int? maxBase64Length = null,
        int? maxBinarySizeBytes = null,
        TargetFormat targetFormat = TargetFormat.WebP,
        int maxSize = 0,
        int initialQuality = 90,
        int minQuality = 30)
    {
        using var fs = File.OpenRead(inputPath);
        var (binary, _) = OptimizeCore(fs, maxBase64Length, maxBinarySizeBytes,
            targetFormat, maxSize, initialQuality, minQuality);
        return binary;
    }

    public static string OptimizeToBase64(
        string inputPath,
        int? maxBase64Length = null,
        int? maxBinarySizeBytes = null,
        TargetFormat targetFormat = TargetFormat.WebP,
        int maxSize = 0,
        int initialQuality = 90,
        int minQuality = 30)
    {
        using var fs = File.OpenRead(inputPath);
        var (binary, base64Length) = OptimizeCore(fs, maxBase64Length, maxBinarySizeBytes,
            targetFormat, maxSize, initialQuality, minQuality);
        return Convert.ToBase64String(binary);
    }

    public static string OptimizeToDataUri(
        string inputPath,
        int? maxBase64Length = null,
        int? maxBinarySizeBytes = null,
        TargetFormat targetFormat = TargetFormat.WebP,
        int maxSize = 0,
        int initialQuality = 90,
        int minQuality = 30)
    {
        using var fs = File.OpenRead(inputPath);
        var (binary, base64Length) = OptimizeCore(fs, maxBase64Length, maxBinarySizeBytes,
            targetFormat, maxSize, initialQuality, minQuality);
        string mimeType = targetFormat switch
        {
            TargetFormat.Jpeg => "image/jpeg",
            TargetFormat.WebP => "image/webp",
            _ => "application/octet-stream"
        };
        return $"{mimeType};base64,{Convert.ToBase64String(binary)}";
    }

    private static (byte[] BinaryData, int Base64Length) OptimizeCore(
        Stream inputStream,
        int? maxBase64Length,
        int? maxBinarySizeBytes,
        TargetFormat targetFormat,
        int maxSize,
        int initialQuality,
        int minQuality)
    {
        ValidateLimits(maxBase64Length, maxBinarySizeBytes);

        using var originalImage = Image.Load(inputStream);
        using var image = originalImage.Clone(ctx => { });

        if (maxSize > 0)
        {
            var currentMaxSide = Math.Max(image.Width, image.Height);
            if (currentMaxSide > maxSize)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(maxSize, maxSize),
                    Mode = ResizeMode.Max
                }));
            }
        }

        int low = minQuality;
        int high = Math.Min(100, Math.Max(minQuality, initialQuality));
        int bestQuality = -1;

        while (low <= high)
        {
            int mid = (low + high) / 2;
            var result = TryEncode(image, targetFormat, mid);

            if (IsWithinLimits(result, maxBase64Length, maxBinarySizeBytes))
            {
                bestQuality = mid;
                low = mid + 5;
            }
            else
            {
                high = mid - 5;
            }
        }

        if (bestQuality != -1)
        {
            var result = TryEncode(image, targetFormat, bestQuality);
            return (result.BinaryData, result.Base64Length);
        }

        if (maxSize == 0)
        {
            var fallbackSizes = new[] { 800, 600, 400 };
            foreach (var size in fallbackSizes)
            {
                using var resized = originalImage.Clone(ctx => { });
                resized.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(size, size),
                    Mode = ResizeMode.Max
                }));

                low = minQuality;
                high = Math.Max(50, minQuality);

                while (low <= high)
                {
                    int mid = (low + high) / 2;
                    var result = TryEncode(resized, targetFormat, mid);

                    if (IsWithinLimits(result, maxBase64Length, maxBinarySizeBytes))
                    {
                        return (result.BinaryData, result.Base64Length);
                    }
                    else
                    {
                        high = mid - 5;
                    }
                }
            }
        }

        throw new InvalidOperationException(
            $"Failed to compress image to satisfy limits. Tried down to quality={minQuality} and size=400px.");
    }

    private static EncodeResult TryEncode(Image image, TargetFormat format, int quality)
    {
        using var ms = new MemoryStream();
        IImageEncoder encoder = format switch
        {
            TargetFormat.Jpeg => new JpegEncoder { Quality = quality },
            TargetFormat.WebP => new WebpEncoder { Quality = quality, FileFormat = WebpFileFormatType.Lossy },
            _ => throw new NotSupportedException($"Format {format} is not supported")
        };

        image.Save(ms, encoder);
        var binary = ms.ToArray();
        int base64Length = (binary.Length + 2) / 3 * 4;
        return new EncodeResult(binary, base64Length);
    }

    private static bool IsWithinLimits(
        EncodeResult result,
        int? maxBase64Length,
        int? maxBinarySizeBytes)
    {
        bool base64Ok = !maxBase64Length.HasValue || result.Base64Length <= maxBase64Length.Value;
        bool binaryOk = !maxBinarySizeBytes.HasValue || result.BinaryData.Length <= maxBinarySizeBytes.Value;
        return base64Ok && binaryOk;
    }

    private static int GetBase64EncodedLength(int binaryLength)
    {
        return (binaryLength + 2) / 3 * 4;
    }

    private static void ValidateLimits(int? maxBase64, int? maxBinary)
    {
        if (!maxBase64.HasValue && !maxBinary.HasValue)
            throw new ArgumentException("At least one of maxBase64Length or maxBinarySizeBytes must be specified.");
        if (maxBase64.HasValue && maxBase64 <= 0)
            throw new ArgumentException("maxBase64Length must be > 0", nameof(maxBase64));
        if (maxBinary.HasValue && maxBinary <= 0)
            throw new ArgumentException("maxBinarySizeBytes must be > 0", nameof(maxBinary));
    }
}