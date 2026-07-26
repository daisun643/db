using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;

namespace Backend.Services;

internal static class MediaStorageShared
{
    public const long MaxImageBytes = 5L * 1024 * 1024;

    public static readonly Dictionary<string, string> AllowedImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/gif"] = ".gif",
        ["image/webp"] = ".webp"
    };

    public static string BuildFileName(IFormFile file)
    {
        if (!AllowedImageTypes.TryGetValue(file.ContentType, out var extension))
            throw new InvalidOperationException("仅支持 JPG、PNG、GIF、WebP 图片");

        return $"{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{extension}";
    }

    public static async Task<byte[]> ReadAndValidateImageBytesAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
            throw new InvalidOperationException("文件不能为空");

        if (file.Length > MaxImageBytes)
            throw new InvalidOperationException("图片文件不能超过5MB");

        if (!AllowedImageTypes.TryGetValue(file.ContentType, out var extension))
            throw new InvalidOperationException("仅支持 JPG、PNG、GIF、WebP 图片");

        await using var readStream = file.OpenReadStream();
        using var memoryStream = new MemoryStream();
        await readStream.CopyToAsync(memoryStream);
        var bytes = memoryStream.ToArray();

        if (!HasValidImageSignature(bytes, extension))
            throw new InvalidOperationException("图片文件格式不正确");

        return bytes;
    }

    public static bool TryGetObjectKeyFromUploadsUrl(string? url, out string objectKey)
    {
        objectKey = string.Empty;
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (!url.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            return false;

        objectKey = url["/uploads/".Length..];
        return !string.IsNullOrWhiteSpace(objectKey);
    }

    public static string? NormalizeBucket(string? bucket)
    {
        return bucket?.Trim().ToLowerInvariant() switch
        {
            "posts" => "posts",
            "products" => "products",
            "avatars" => "avatars",
            _ => null
        };
    }

    public static bool IsSafeObjectKey(string objectKey)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
            return false;

        if (objectKey.Contains("..") || objectKey.Contains("\\"))
            return false;

        if (objectKey.Contains('\0'))
            return false;

        return true;
    }

    public static string GetContentType(string fileNameOrPath)
    {
        var extension = Path.GetExtension(fileNameOrPath).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }

    private static bool HasValidImageSignature(byte[] bytes, string extension)
    {
        if (bytes.Length < 4)
            return false;

        return extension switch
        {
            ".jpg" => bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
            ".png" => bytes.Length >= 8 &&
                      bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
                      bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A,
            ".gif" => bytes.Length >= 6 &&
                      bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 &&
                      bytes[3] == 0x38 && (bytes[4] == 0x37 || bytes[4] == 0x39) && bytes[5] == 0x61,
            ".webp" => bytes.Length >= 12 &&
                       bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
                       bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50,
            _ => false
        };
    }
}
