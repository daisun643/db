using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MediaController : ControllerBase
{
    private readonly IMediaStorageService _mediaStorageService;
    private const int MaxFilesPerRequest = 6;

    public MediaController(IMediaStorageService mediaStorageService)
    {
        _mediaStorageService = mediaStorageService;
    }

    [HttpPost("images")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<UploadImagesResponse>> UploadImages([FromForm] IEnumerable<IFormFile> files, [FromQuery] string bucket = "posts")
    {
        var normalizedBucket = NormalizeBucket(bucket);
        if (string.IsNullOrWhiteSpace(normalizedBucket))
        {
            return BadRequest(new { message = "仅支持 posts、products、avatars 的上传目录" });
        }

        var fileList = files?
            .Where(file => file != null && file.Length > 0)
            .ToList() ?? new List<IFormFile>();

        if (fileList.Count == 0)
            return BadRequest(new { message = "请上传至少一张图片" });

        if (fileList.Count > MaxFilesPerRequest)
        {
            return BadRequest(new { message = "单次最多上传 6 张图片" });
        }

        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        if (userId == 0)
            return Unauthorized();

        var urls = new List<string>();
        var storedImages = new List<StoredImage>();

        foreach (var file in fileList)
        {
            try
            {
                var stored = await _mediaStorageService.StoreImageAsync(file, normalizedBucket, userId);
                storedImages.Add(stored);
                urls.Add(stored.Url);
            }
            catch (InvalidOperationException ex)
            {
                foreach (var image in storedImages)
                {
                    await _mediaStorageService.DeleteByUrlAsync(image.Url);
                }

                return BadRequest(new { message = ex.Message });
            }
        }

        return Ok(new UploadImagesResponse(urls));
    }

    private static string? NormalizeBucket(string? bucket)
    {
        return bucket?.Trim().ToLowerInvariant() switch
        {
            "posts" => "posts",
            "products" => "products",
            "avatars" => "avatars",
            _ => null
        };
    }

    public sealed record UploadImagesResponse(List<string> Urls);
}
