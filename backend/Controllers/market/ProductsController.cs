using Backend.Data;
using Backend.Models;
using Backend.Models.DTOs;
using Backend.Authorization;
using Backend.Services;
using Backend.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICreditService _creditService;

    public ProductsController(AppDbContext db, ICreditService creditService)
    {
        _db = db;
        _creditService = creditService;
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

        return Ok(products.Select(MapProduct).ToList());
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<ProductResponse>> GetById(int id)
    {
        var product = await _db.Products.Include(p => p.User).FirstOrDefaultAsync(p => p.ProductID == id);
        return product is null ? NotFound() : Ok(MapProduct(product));
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

        var product = new Product
        {
            Title = request.Title.Trim(),
            Description = request.Description,
            ImageUrls = SerializeImageUrls(request.ImageUrls),
            Category = NormalizeOptionalText(request.Category, "其他", 50),
            Condition = NormalizeOptionalText(request.Condition, "良好", 50),
            Price = request.Price,
            Stock = request.Stock,
            UserID = userId,
            PublishTime = DateTime.Now,
            Status = "Active"
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = product.ProductID }, MapProduct(product));
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

        product.Title = request.Title.Trim();
        product.Description = request.Description;
        product.ImageUrls = SerializeImageUrls(request.ImageUrls);
        product.Category = NormalizeOptionalText(request.Category, "其他", 50);
        product.Condition = NormalizeOptionalText(request.Condition, "良好", 50);
        product.Price = request.Price;
        product.Stock = request.Stock;
        product.Status = NormalizeProductStatusForUpdate(request.Status, request.Stock);

        await _db.SaveChangesAsync();
        return Ok(MapProduct(product));
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

        return Ok(products.Select(MapProduct).ToList());
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

    private static ProductResponse MapProduct(Product product)
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
            ImageUrls = DeserializeImageUrls(product.ImageUrls)
        };
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

    private static string NormalizeOptionalText(string? value, string fallback, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string SerializeImageUrls(IEnumerable<string> urls)
    {
        var normalized = urls
            .Select(url => url.Trim())
            .Where(url => Uri.TryCreate(url, UriKind.Absolute, out var parsed) &&
                (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();

        return JsonSerializer.Serialize(normalized);
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
}
