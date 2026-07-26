using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Backend.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Security.Cryptography;

namespace Backend.Services;

public class S3MediaStorageService : IMediaStorageService, IAsyncDisposable
{
    private readonly MediaStorageS3Settings _settings;
    private readonly ILogger<S3MediaStorageService> _logger;
    private readonly IAmazonS3 _client;
    private readonly SemaphoreSlim _bucketLock = new(1, 1);
    private bool _bucketEnsured;

    public S3MediaStorageService(
        IOptions<MediaStorageSettings> mediaStorageOptions,
        ILogger<S3MediaStorageService> logger)
    {
        _settings = mediaStorageOptions.Value.S3;
        _logger = logger;

        var config = new AmazonS3Config
        {
            ServiceURL = BuildServiceUrl(_settings),
            ForcePathStyle = true,
            AuthenticationRegion = string.IsNullOrWhiteSpace(_settings.Region) ? "us-east-1" : _settings.Region
        };

        if (!string.IsNullOrWhiteSpace(config.AuthenticationRegion))
        {
            _logger.LogInformation("S3 AuthenticationRegion={AuthenticationRegion}, ServiceURL={ServiceURL}", config.AuthenticationRegion, config.ServiceURL);
        }

        _client = new AmazonS3Client(
            new BasicAWSCredentials(_settings.AccessKey, _settings.SecretKey),
            config);
    }

    public async Task<StoredImage> StoreImageAsync(IFormFile file, string bucket, int? uploadedByUserId = null)
    {
        var bytes = await MediaStorageShared.ReadAndValidateImageBytesAsync(file);

        var normalizedBucket = MediaStorageShared.NormalizeBucket(bucket)
            ?? throw new InvalidOperationException("仅支持 posts、products、avatars 的上传目录");
        var extension = MediaStorageShared.AllowedImageTypes[file!.ContentType];
        var fileName = $"{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{extension}";
        var objectKey = $"{normalizedBucket}/{fileName}";

        await EnsureBucketAsync();

        using var stream = new MemoryStream(bytes);
        var putRequest = new PutObjectRequest
        {
            BucketName = _settings.Bucket,
            Key = objectKey,
            InputStream = stream,
            ContentType = file.ContentType,
            AutoCloseStream = false
        };

        var response = await _client.PutObjectAsync(putRequest);
        if (response.HttpStatusCode != HttpStatusCode.OK && response.HttpStatusCode != HttpStatusCode.NoContent)
            throw new InvalidOperationException("图片存储失败");

        var hashBytes = SHA256.HashData(bytes);
        var contentHash = BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToLowerInvariant();

        return new StoredImage(
            Url: BuildPublicUrl(objectKey),
            FileName: fileName,
            OriginalFileName: file.FileName,
            MimeType: file.ContentType,
            SizeBytes: bytes.Length,
            ContentHash: contentHash,
            StorageProvider: "s3",
            ObjectKey: objectKey,
            UploadedByUserId: uploadedByUserId);
    }

    public async Task<StoredMediaObject?> ReadObjectAsync(string objectKey)
    {
        if (!MediaStorageShared.IsSafeObjectKey(objectKey))
            return null;

        try
        {
            await EnsureBucketAsync();
            using var response = await _client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = _settings.Bucket,
                Key = objectKey
            });

            if (response.HttpStatusCode != HttpStatusCode.OK)
            {
                return null;
            }

            using var ms = new MemoryStream();
            await response.ResponseStream.CopyToAsync(ms);
            return new StoredMediaObject(
                Content: ms.ToArray(),
                ContentType: string.IsNullOrWhiteSpace(response.Headers.ContentType)
                    ? MediaStorageShared.GetContentType(objectKey)
                    : response.Headers.ContentType,
                FileName: Path.GetFileName(objectKey),
                ObjectKey: objectKey);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning(ex, "S3 object not found: {ObjectKey}", objectKey);
            return null;
        }
    }

    public async Task DeleteByUrlAsync(string? url)
    {
        if (!MediaStorageShared.TryGetObjectKeyFromUploadsUrl(url, out var objectKey))
            return;

        try
        {
            await EnsureBucketAsync();
            await _client.DeleteObjectAsync(_settings.Bucket, objectKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除对象失败: {ObjectKey}", objectKey);
        }
    }

    private string BuildPublicUrl(string objectKey)
    {
        if (!string.IsNullOrWhiteSpace(_settings.PublicBaseUrl))
        {
            return $"{_settings.PublicBaseUrl.TrimEnd('/')}/{_settings.Bucket}/{objectKey}";
        }

        return $"/uploads/{objectKey}";
    }

    private static string BuildServiceUrl(MediaStorageS3Settings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Endpoint))
        {
            return "http://localhost:9000";
        }

        if (settings.Endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            settings.Endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return settings.Endpoint;
        }

        var scheme = settings.UseSsl ? "https" : "http";
        return $"{scheme}://{settings.Endpoint}";
    }

    private async Task EnsureBucketAsync()
    {
        if (_bucketEnsured)
            return;

        await _bucketLock.WaitAsync();
        try
        {
            if (_bucketEnsured)
                return;

            try
            {
                await _client.PutBucketAsync(_settings.Bucket);
            }
            catch (AmazonS3Exception ex)
            {
                if (ex.ErrorCode == "BucketAlreadyOwnedByYou" ||
                    ex.ErrorCode == "BucketAlreadyExists")
                {
                    _bucketEnsured = true;
                    return;
                }

                _logger.LogError(
                    ex,
                    "S3 PutBucket 失败: Endpoint={Endpoint}, Bucket={Bucket}, ErrorCode={ErrorCode}, StatusCode={StatusCode}, RequestId={RequestId}",
                    _client.Config?.ServiceURL,
                    _settings.Bucket,
                    ex.ErrorCode,
                    ex.StatusCode,
                    ex.RequestId);

                throw;
            }

            _bucketEnsured = true;
        }
        finally
        {
            _bucketLock.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        _bucketLock.Dispose();
        return ValueTask.CompletedTask;
    }
}
