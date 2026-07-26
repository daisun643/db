using Microsoft.AspNetCore.Http;

namespace Backend.Services;

public interface IMediaStorageService
{
    Task<StoredImage> StoreImageAsync(IFormFile file, string bucket, int? uploadedByUserId = null);

    Task DeleteByUrlAsync(string? url);

    Task<StoredMediaObject?> ReadObjectAsync(string objectKey);
}

public sealed record StoredImage(
    string Url,
    string FileName,
    string OriginalFileName,
    string MimeType,
    long SizeBytes,
    string? ContentHash,
    string StorageProvider,
    string ObjectKey,
    int? UploadedByUserId);

public sealed record StoredMediaObject(
    byte[] Content,
    string ContentType,
    string FileName,
    string? ObjectKey = null);
