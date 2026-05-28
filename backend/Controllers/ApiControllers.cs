using Backend.Data;
using Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;

    public UsersController(AppDbContext db) => _db = db;

    [HttpGet]
    [Authorize(Roles = "Admin,Moderator")]
    public async Task<ActionResult<List<User>>> GetAll()
    {
        return await _db.Users.ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<User>> GetById(int id)
    {
        var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var isAdmin = User.IsInRole("Admin");
        
        if (id != currentUserId && !isAdmin)
            return Forbid();

        var user = await _db.Users.FindAsync(id);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<User>> Create(User user)
    {
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = user.UserID }, user);
    }
}

[ApiController]
[Route("api/[controller]")]
public class ForumsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ForumsController(AppDbContext db) => _db = db;

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<Forum>>> GetAll()
    {
        return await _db.Forums.ToListAsync();
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<Forum>> GetById(int id)
    {
        var forum = await _db.Forums.FindAsync(id);
        return forum is null ? NotFound() : Ok(forum);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<Forum>> Create([FromBody] Forum forum)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        forum.CreatorID = userId;
        forum.CreateTime = DateTime.Now;
        forum.Status = "Active";

        _db.Forums.Add(forum);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = forum.ForumID }, forum);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Moderator")]
    public async Task<ActionResult<Forum>> Update(int id, [FromBody] Forum updatedForum)
    {
        var forum = await _db.Forums.FindAsync(id);
        if (forum == null)
            return NotFound();

        forum.ForumName = updatedForum.ForumName;
        forum.Description = updatedForum.Description;
        forum.Status = updatedForum.Status;

        await _db.SaveChangesAsync();
        return Ok(forum);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Delete(int id)
    {
        var forum = await _db.Forums.FindAsync(id);
        if (forum == null)
            return NotFound();

        _db.Forums.Remove(forum);
        await _db.SaveChangesAsync();
        return Ok(new { message = "删除成功" });
    }
}

[ApiController]
[Route("api/[controller]")]
public class PostsController : ControllerBase
{
    private readonly AppDbContext _db;

    public PostsController(AppDbContext db) => _db = db;

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<Post>>> GetAll()
    {
        return await _db.Posts.Include(p => p.User).ToListAsync();
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<Post>> GetById(int id)
    {
        var post = await _db.Posts.Include(p => p.User).Include(p => p.Forum)
            .FirstOrDefaultAsync(p => p.PostID == id);
        return post is null ? NotFound() : Ok(post);
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<Post>> Create([FromBody] Post post)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        post.UserID = userId;
        post.CreateTime = DateTime.Now;
        post.UpdateTime = DateTime.Now;
        post.Status = "Active";
        post.LikeCount = 0;
        post.ViewCount = 0;
        post.HeatScore = 0;

        _db.Posts.Add(post);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = post.PostID }, post);
    }

    [HttpPut("{id}")]
    [Authorize]
    public async Task<ActionResult<Post>> Update(int id, [FromBody] Post updatedPost)
    {
        var post = await _db.Posts.FindAsync(id);
        if (post == null)
            return NotFound();

        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var isModerator = User.IsInRole("Admin") || User.IsInRole("Moderator");

        if (post.UserID != userId && !isModerator)
            return Forbid();

        post.Title = updatedPost.Title;
        post.Content = updatedPost.Content;
        post.UpdateTime = DateTime.Now;

        await _db.SaveChangesAsync();
        return Ok(post);
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<ActionResult> Delete(int id)
    {
        var post = await _db.Posts.FindAsync(id);
        if (post == null)
            return NotFound();

        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var isModerator = User.IsInRole("Admin") || User.IsInRole("Moderator");

        if (post.UserID != userId && !isModerator)
            return Forbid();

        _db.Posts.Remove(post);
        await _db.SaveChangesAsync();
        return Ok(new { message = "删除成功" });
    }
}

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProductsController(AppDbContext db) => _db = db;

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<Product>>> GetAll()
    {
        return await _db.Products.ToListAsync();
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<Product>> GetById(int id)
    {
        var product = await _db.Products.FindAsync(id);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<Product>> Create([FromBody] Product product)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        product.UserID = userId;
        product.PublishTime = DateTime.Now;
        product.Status = "Active";

        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = product.ProductID }, product);
    }

    [HttpPut("{id}")]
    [Authorize]
    public async Task<ActionResult<Product>> Update(int id, [FromBody] Product updatedProduct)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null)
            return NotFound();

        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var isAdmin = User.IsInRole("Admin");

        if (product.UserID != userId && !isAdmin)
            return Forbid();

        product.Title = updatedProduct.Title;
        product.Description = updatedProduct.Description;
        product.Price = updatedProduct.Price;
        product.Stock = updatedProduct.Stock;
        product.Status = updatedProduct.Status;

        await _db.SaveChangesAsync();
        return Ok(product);
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<ActionResult> Delete(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null)
            return NotFound();

        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var isAdmin = User.IsInRole("Admin");

        if (product.UserID != userId && !isAdmin)
            return Forbid();

        _db.Products.Remove(product);
        await _db.SaveChangesAsync();
        return Ok(new { message = "删除成功" });
    }
}

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly AppDbContext _db;

    public HealthController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        try
        {
            await _db.Database.CanConnectAsync();
            return Ok(new { status = "healthy", database = "connected" });
        }
        catch (Exception ex)
        {
            return StatusCode(503, new { status = "unhealthy", error = ex.Message });
        }
    }
}
