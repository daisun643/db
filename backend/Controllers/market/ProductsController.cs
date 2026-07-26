using Backend.Data;
using Backend.Models;
using Backend.Models.DTOs;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICreditService _creditService;
    private readonly IMediaStorageService _mediaStorageService;
    private const string OwnerType = "Product";
    private const int MaxImageCount = 6;

    public ProductsController(AppDbContext db, ICreditService creditService, IMediaStorageService mediaStorageService)
    {
        _db = db;
        _creditService = creditService;
        _mediaStorageService = mediaStorageService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<ProductResponse>>> GetAll([FromQuery] string? status)
    {
        var query = _db.Products.Include(p => p.User).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(p => p.Status == status);
        else
            query = query.Where(p => p.Status != "Inactive");

        var products = await query
            .OrderByDescending(p => p.PublishTime)
            .ToListAsync();

        return Ok(await MapProductListAsync(products));
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<ProductResponse>> GetById(int id)
    {
        var product = await _db.Products.Include(p => p.User).FirstOrDefaultAsync(p => p.ProductID == id);
        if (product is null)
            return NotFound();

        return Ok(await MapProductAsync(product));
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<ProductResponse>> Create([FromBody] CreateProductRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        if (!User.IsInRole("Admin") && !HasProductPermission("products.create"))
            return Forbid();

        if (!await _creditService.CanPerformAsync(userId, "product.publish"))
            return BadRequest(new { message = "信用分不足或账号不可用，暂不能发布商品" });

        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            return Forbid();

        var normalizedImageUrls = NormalizeImageUrls(request.ImageUrls);
        var product = new Product
        {
            Title = request.Title.Trim(),
            Description = request.Description,
            ImageUrls = SerializeImageUrls(normalizedImageUrls),
            Price = request.Price,
            Stock = request.Stock,
            UserID = userId,
            PublishTime = DateTime.Now,
            Status = "Active"
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        await ReplaceMediaForOwnerAsync(product.ProductID, userId, normalizedImageUrls);
        await _db.SaveChangesAsync();

        var mapped = await MapProductAsync(product);
        return CreatedAtAction(nameof(GetById), new { id = product.ProductID }, mapped);
    }

    [HttpPut("{id}")]
    [Authorize]
    public async Task<ActionResult<ProductResponse>> Update(int id, [FromBody] UpdateProductRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var product = await _db.Products.FindAsync(id);
        if (product == null)
            return NotFound();

        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

        if (!CanManageProduct(product, userId, "products.edit"))
            return Forbid();

        if (product.Status is "Sold" or "Inactive")
            return BadRequest(new { message = "已售出或已下架商品不可编辑，请先重新上架符合条件的下架商品" });

        if (product.Status == "Locked")
            return BadRequest(new { message = "已锁定商品存在待处理订单，不可编辑" });

        var normalizedImageUrls = NormalizeImageUrls(request.ImageUrls);
        product.Title = request.Title.Trim();
        product.Description = request.Description;
        product.ImageUrls = SerializeImageUrls(normalizedImageUrls);
        product.Price = request.Price;
        product.Stock = request.Stock;
        product.Status = NormalizeProductStatusForUpdate(request.Status, request.Stock);

        await ReplaceMediaForOwnerAsync(product.ProductID, userId, normalizedImageUrls);
        await _db.SaveChangesAsync();

        return Ok(await MapProductAsync(product));
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<ActionResult> Delete(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null)
            return NotFound();

        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

        if (!CanManageProduct(product, userId, "products.delete"))
            return Forbid();

        product.Status = "Inactive";
        await _db.SaveChangesAsync();
        return Ok(new { message = "已下架" });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<List<ProductResponse>>> GetMyProducts()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var products = await _db.Products
            .Include(p => p.User)
            .Where(p => p.UserID == userId)
            .OrderByDescending(p => p.PublishTime)
            .ToListAsync();

        return Ok(await MapProductListAsync(products));
    }

    [HttpPost("{id}/status")]
    [Authorize]
    public async Task<ActionResult> ChangeStatus(int id, [FromBody] ChangeProductStatusRequest request)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null)
            return NotFound();

        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var action = request.Action.Trim().ToLowerInvariant();
        if (action is not "publish" and not "lock" and not "sold" and not "inactive" and not "off-shelf" and not "restore")
            return BadRequest(new { message = "状态动作不合法" });

        var requiredPermission = action is "inactive" or "off-shelf" ? "products.delete" : "products.edit";
        if (!CanManageProduct(product, userId, requiredPermission))
            return Forbid();

        if (action is "publish" or "restore")
        {
            if (product.Status == "Sold")
                return BadRequest(new { message = "售出商品不能重新上架" });
            if ((product.Stock ?? 0) <= 0)
                return BadRequest(new { message = "库存不足，不能上架商品" });
        }

        if (action is "inactive" or "off-shelf" && product.Status == "Sold")
            return BadRequest(new { message = "售出商品不能下架" });

        product.Status = action switch
        {
            "publish" => product.Stock > 0 ? "Active" : "Sold",
            "lock" => product.Status == "Sold" ? "Sold" : "Locked",
            "sold" => "Sold",
            "inactive" or "off-shelf" => "Inactive",
            "restore" => product.Stock > 0 ? "Active" : "Sold",
            _ => product.Status
        };

        await _db.SaveChangesAsync();
        return Ok(new { message = "状态已更新", status = product.Status });
    }

    private async Task<List<ProductResponse>> MapProductListAsync(IEnumerable<Product> products)
    {
        var productList = products.ToList();
        var mediaByProduct = await GetMediaByProductIdsAsync(productList.Select(p => p.ProductID));
        return productList.Select(p => MapProduct(p, mediaByProduct)).ToList();
    }

    private async Task<ProductResponse> MapProductAsync(Product product)
    {
        var mapped = await MapProductListAsync(new[] { product });
        return mapped.Single();
    }

    private static ProductResponse MapProduct(Product product, Dictionary<int, List<string>> mediaByProduct)
    {
        return new ProductResponse
        {
            ProductID = product.ProductID,
            Title = product.Title ?? "",
            Description = product.Description ?? "",
            Price = product.Price ?? 0,
            Stock = product.Stock ?? 0,
            Category = product.Category ?? "",
            Condition = product.Condition ?? "",
            Status = product.Status ?? "",
            PublishTime = product.PublishTime,
            UserID = product.UserID,
            SellerName = product.User?.Username ?? "",
            ImageUrls = ResolveImageUrls(mediaByProduct, product)
        };
    }

    private async Task<Dictionary<int, List<string>>> GetMediaByProductIdsAsync(IEnumerable<int> productIds)
    {
        var ids = productIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            return new Dictionary<int, List<string>>();

        var mediaRows = await _db.MediaFiles
            .Where(m => m.OwnerType == OwnerType &&
                        m.OwnerID.HasValue &&
                        ids.Contains(m.OwnerID.Value))
            .OrderBy(m => m.DisplayOrder ?? int.MaxValue)
            .ThenBy(m => m.MediaID)
            .ToListAsync();

        return mediaRows
            .Where(m => m.OwnerID.HasValue)
            .GroupBy(m => m.OwnerID!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.Select(m => m.Url).Where(HasUrl).ToList());
    }

    private static string SerializeImageUrls(IEnumerable<string> urls)
    {
        var normalized = NormalizeImageUrls(urls);
        return JsonSerializer.Serialize(normalized.Take(MaxImageCount).ToList());
    }

    private static List<string> NormalizeImageUrls(IEnumerable<string>? urls)
    {
        if (urls == null)
            return new List<string>();

        return urls
            .Select(url => url?.Trim())
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Where(IsValidImageUrl)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxImageCount)
            .ToList();
    }

    private static bool IsValidImageUrl(string url)
    {
        if (url.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            return true;

        return Uri.TryCreate(url, UriKind.Absolute, out var parsed) &&
            (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps);
    }

    private static bool HasUrl(string? url)
    {
        return !string.IsNullOrWhiteSpace(url);
    }

    private static List<string> ResolveImageUrls(Dictionary<int, List<string>> mediaByProduct, Product product)
    {
        if (product.ProductID != 0 &&
            mediaByProduct.TryGetValue(product.ProductID, out var mediaImageUrls) &&
            mediaImageUrls.Count > 0)
        {
            return mediaImageUrls.Take(MaxImageCount).ToList();
        }

        return DeserializeImageUrls(product.ImageUrls);
    }

    private static List<string> DeserializeImageUrls(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new List<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(value) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private bool HasProductPermission(string permission)
    {
        return User.Claims.Any(c => c.Type == "Permission" &&
            string.Equals(c.Value, permission, StringComparison.OrdinalIgnoreCase));
    }

    private bool CanManageProduct(Product product, int userId, string permission)
    {
        return product.UserID == userId ||
            User.IsInRole("Admin") ||
            HasProductPermission(permission);
    }

    private static string NormalizeProductStatusForUpdate(string status, int stock)
    {
        if (stock <= 0)
            return "Sold";

        return status switch
        {
            "Active" => status,
            _ => "Active"
        };
    }

    private async Task ReplaceMediaForOwnerAsync(int ownerId, int? uploadedByUserId, IEnumerable<string> imageUrls)
    {
        var normalized = NormalizeImageUrls(imageUrls);

        var existing = await _db.MediaFiles
            .Where(m => m.OwnerType == OwnerType && m.OwnerID == ownerId)
            .ToListAsync();

        var existingLookup = existing
            .Where(x => !string.IsNullOrWhiteSpace(x.Url))
            .ToDictionary(x => x.Url!, x => x, StringComparer.OrdinalIgnoreCase);

        var incomingSet = normalized.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var removed in existing
            .Where(m => !incomingSet.Contains(m.Url ?? ""))
            .ToList())
        {
            await _mediaStorageService.DeleteByUrlAsync(removed.Url);
            _db.MediaFiles.Remove(removed);
        }

        var existingRetained = existing
            .Where(m => incomingSet.Contains(m.Url ?? ""))
            .ToDictionary(x => x.Url!, x => x, StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < normalized.Count; i++)
        {
            var url = normalized[i];
            if (existingRetained.TryGetValue(url, out var matched))
            {
                matched.DisplayOrder = i;
                matched.UploadTime = DateTime.UtcNow;
                continue;
            }

            if (existingLookup.TryGetValue(url, out var item))
            {
                item.OwnerID = ownerId;
                item.DisplayOrder = i;
                item.UploadTime = DateTime.UtcNow;
                continue;
            }

            _db.MediaFiles.Add(new MediaFile
            {
                OwnerType = OwnerType,
                OwnerID = ownerId,
                StorageProvider = url.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase) ? "s3" : "external",
                Url = url,
                UploadedByUserID = uploadedByUserId,
                DisplayOrder = i,
                UploadTime = DateTime.UtcNow
            });
        }
    }
}
