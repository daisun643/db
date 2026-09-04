using Backend.Data;
using Backend.Models;
using Backend.Models.DTOs;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICreditService _creditService;
    private readonly IMediaStorageService _mediaStorageService;
    private const int MaxImageCount = 6;

    public ProductsController(AppDbContext db, ICreditService creditService, IMediaStorageService mediaStorageService)
    {
        _db = db;
        _creditService = creditService;
        _mediaStorageService = mediaStorageService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<ProductResponse>>> GetAll(
        [FromQuery] string? status,
        [FromQuery] string? keyword,
        [FromQuery] string? category,
        [FromQuery] string? condition,
        [FromQuery] int? sellerId,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] int? minStock,
        [FromQuery] int? maxStock,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? sort = "latest",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var normalizedSort = (sort ?? "latest").Trim().ToLowerInvariant();
        var normalizedStatus = status?.Trim();

        if (normalizedSort is not "latest" and not "price-asc" and not "price-desc" and not "stock-asc" and not "stock-desc")
            return BadRequest(new { message = "排序参数不合法" });

        if (from.HasValue && to.HasValue && from.Value > to.Value)
            return BadRequest(new { message = "时间范围不合法" });

        if (minPrice < 0 || maxPrice < 0 ||
            (minPrice.HasValue && maxPrice.HasValue && minPrice.Value > maxPrice.Value))
            return BadRequest(new { message = "价格范围不合法" });

        if (minStock < 0 || maxStock < 0 ||
            (minStock.HasValue && maxStock.HasValue && minStock.Value > maxStock.Value))
            return BadRequest(new { message = "库存范围不合法" });

        var query = _db.Products
            .Include(p => p.User)
            .Include(p => p.Category)
            .Include(p => p.Condition)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var trimmedKeyword = keyword.Trim();
            query = query.Where(p =>
                (p.Title != null && p.Title.Contains(trimmedKeyword)) ||
                (p.Description != null && p.Description.Contains(trimmedKeyword)) ||
                (p.User != null && p.User.Username != null && p.User.Username.Contains(trimmedKeyword)));
        }

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(p => p.Category != null && p.Category.CategoryName == category.Trim());

        if (!string.IsNullOrWhiteSpace(condition))
            query = query.Where(p => p.Condition != null && p.Condition.ConditionName == condition.Trim());

        if (sellerId.HasValue)
            query = query.Where(p => p.UserID == sellerId.Value);

        if (minPrice.HasValue)
            query = query.Where(p => (p.Price ?? 0) >= minPrice.Value);

        if (maxPrice.HasValue)
            query = query.Where(p => (p.Price ?? 0) <= maxPrice.Value);

        if (minStock.HasValue)
            query = query.Where(p => (p.Stock ?? 0) >= minStock.Value);

        if (maxStock.HasValue)
            query = query.Where(p => (p.Stock ?? 0) <= maxStock.Value);

        if (from.HasValue)
            query = query.Where(p => p.PublishTime >= from.Value);

        if (to.HasValue)
            query = query.Where(p => p.PublishTime <= to.Value);

        if (!string.IsNullOrWhiteSpace(normalizedStatus))
        {
            var knownStatus = NormalizeProductStatus(normalizedStatus);
            if (knownStatus == null)
                return BadRequest(new { message = "商品状态不合法" });
            query = query.Where(p => p.Status == knownStatus);
        }
        else
            query = query.Where(p => p.Status != "Inactive");

        query = normalizedSort switch
        {
            "price-asc" => query.OrderBy(p => p.Price ?? 0).ThenByDescending(p => p.PublishTime).ThenByDescending(p => p.ProductID),
            "price-desc" => query.OrderByDescending(p => p.Price ?? 0).ThenByDescending(p => p.PublishTime).ThenByDescending(p => p.ProductID),
            "stock-asc" => query.OrderBy(p => p.Stock ?? 0).ThenByDescending(p => p.PublishTime).ThenByDescending(p => p.ProductID),
            "stock-desc" => query.OrderByDescending(p => p.Stock ?? 0).ThenByDescending(p => p.PublishTime).ThenByDescending(p => p.ProductID),
            _ => query.OrderByDescending(p => p.Status == "Active" ? 1 : 0)
                    .ThenByDescending(p => p.PublishTime)
                    .ThenByDescending(p => p.ProductID),
        };

        var totalCount = await query.CountAsync();
        var products = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        Response.Headers["X-Total-Count"] = totalCount.ToString();
        return Ok(await MapProductListAsync(products));
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<ProductResponse>> GetById(int id)
    {
        var product = await _db.Products
            .Include(p => p.User)
            .Include(p => p.Category)
            .Include(p => p.Condition)
            .FirstOrDefaultAsync(p => p.ProductID == id);
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
        if (!User.IsInRole("Manager") && !HasProductPermission("products.create"))
            return Forbid();

        if (!await _creditService.CanPerformAsync(userId, "product.publish"))
            return BadRequest(new { message = "信用分不足或账号不可用，暂不能发布商品" });

        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            return Forbid();

        var normalizedImageUrls = NormalizeImageUrls(request.ImageUrls);
        var category = NormalizeProductText(request.Category, "其他");
        var condition = NormalizeProductText(request.Condition, "良好");
        var categoryRow = await _db.ProductCategories.SingleOrDefaultAsync(x => x.CategoryName == category);
        var conditionRow = await _db.ProductConditions.SingleOrDefaultAsync(x => x.ConditionName == condition);
        if (categoryRow == null || conditionRow == null)
            return BadRequest(new { message = "商品分类或成色不在预置字典中" });
        var product = new Product
        {
            Title = request.Title.Trim(),
            Description = request.Description,
            CategoryID = categoryRow.CategoryID,
            ConditionID = conditionRow.ConditionID,
            Category = categoryRow,
            Condition = conditionRow,
            Price = request.Price,
            Stock = request.Stock,
            UserID = userId,
            PublishTime = DateTime.Now,
            Status = "Active"
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        await ReplaceProductMediaAsync(product.ProductID, userId, normalizedImageUrls);
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
        var categoryName = NormalizeProductText(request.Category, "其他");
        var conditionName = NormalizeProductText(request.Condition, "良好");
        var categoryRow = await _db.ProductCategories.SingleOrDefaultAsync(x => x.CategoryName == categoryName);
        var conditionRow = await _db.ProductConditions.SingleOrDefaultAsync(x => x.ConditionName == conditionName);
        if (categoryRow == null || conditionRow == null)
            return BadRequest(new { message = "商品分类或成色不在预置字典中" });
        product.Title = request.Title.Trim();
        product.Description = request.Description;
        product.CategoryID = categoryRow.CategoryID;
        product.ConditionID = conditionRow.ConditionID;
        product.Category = categoryRow;
        product.Condition = conditionRow;
        product.Price = request.Price;
        product.Stock = request.Stock;
        product.Status = NormalizeProductStatusForUpdate(request.Status, request.Stock);

        await ReplaceProductMediaAsync(product.ProductID, userId, normalizedImageUrls);
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
            .Include(p => p.Category)
            .Include(p => p.Condition)
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
            Category = product.Category?.CategoryName ?? "其他",
            Condition = product.Condition?.ConditionName ?? "良好",
            Status = product.Status ?? "",
            PublishTime = product.PublishTime,
            UserID = product.UserID,
            SellerName = product.User?.Username ?? "",
            ImageUrls = ResolveImageUrls(mediaByProduct, product)
        };
    }

    private static string NormalizeProductText(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private async Task<Dictionary<int, List<string>>> GetMediaByProductIdsAsync(IEnumerable<int> productIds)
    {
        var ids = productIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            return new Dictionary<int, List<string>>();

        var mediaRows = await _db.ProductMedia
            .Include(x => x.Media)
            .Where(x => ids.Contains(x.ProductID))
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.MediaID)
            .ToListAsync();

        return mediaRows
            .GroupBy(x => x.ProductID)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.Media?.Url).Where(HasUrl).Select(url => url!).ToList());
    }

    private static List<string> NormalizeImageUrls(IEnumerable<string>? urls)
    {
        if (urls == null)
            return new List<string>();

        return urls
            .Select(url => url?.Trim())
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => url!)
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

        return new List<string>();
    }

    private bool HasProductPermission(string permission)
    {
        return User.Claims.Any(c => c.Type == "Permission" &&
            string.Equals(c.Value, permission, StringComparison.OrdinalIgnoreCase));
    }

    private bool CanManageProduct(Product product, int userId, string permission)
    {
        return product.UserID == userId ||
            User.IsInRole("Manager") ||
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

    private static string? NormalizeProductStatus(string status)
    {
        return status.Trim().ToLowerInvariant() switch
        {
            "active" => "Active",
            "locked" => "Locked",
            "sold" => "Sold",
            "inactive" => "Inactive",
            _ => null,
        };
    }

    private async Task ReplaceProductMediaAsync(int productId, int? uploadedByUserId, IEnumerable<string> imageUrls)
    {
        var normalized = NormalizeImageUrls(imageUrls);

        var existingLinks = await _db.ProductMedia
            .Include(x => x.Media)
            .Where(x => x.ProductID == productId)
            .ToListAsync();
        var existingLookup = existingLinks
            .Where(x => !string.IsNullOrWhiteSpace(x.Media?.Url))
            .ToDictionary(x => x.Media!.Url!, x => x.Media!, StringComparer.OrdinalIgnoreCase);

        var incomingSet = normalized.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var removed in existingLinks.Where(x => !incomingSet.Contains(x.Media?.Url ?? "")))
        {
            await _mediaStorageService.DeleteByUrlAsync(removed.Media?.Url);
            if (removed.Media != null)
                _db.MediaFiles.Remove(removed.Media);
        }
        _db.ProductMedia.RemoveRange(existingLinks);

        for (var i = 0; i < normalized.Count; i++)
        {
            var url = normalized[i];
            if (!existingLookup.TryGetValue(url, out var media))
            {
                media = new MediaFile
                {
                    StorageProvider = url.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase) ? "s3" : "external",
                    Url = url,
                    UploadedByUserID = uploadedByUserId,
                    UploadTime = DateTime.UtcNow
                };
                _db.MediaFiles.Add(media);
            }
            _db.ProductMedia.Add(new ProductMedia { ProductID = productId, Media = media, DisplayOrder = i });
        }
    }
}
