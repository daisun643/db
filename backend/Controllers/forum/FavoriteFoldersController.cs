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
[Route("api/favorite-folders")]
[Authorize]
public class FavoriteFoldersController : ControllerBase
{
    private readonly AppDbContext _db;

    public FavoriteFoldersController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<FavoriteFolderResponse>>> GetFolders()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var folders = await _db.FavoriteFolders
            .Where(f => f.UserID == userId)
            .Select(f => new FavoriteFolderResponse
            {
                FolderID = f.FolderID,
                FolderName = f.FolderName ?? "",
                CreateTime = f.CreateTime,
                PostCount = _db.FolderPosts.Count(fp => fp.FolderID == f.FolderID)
            })
            .ToListAsync();

        return Ok(folders);
    }

    [HttpPost]
    public async Task<ActionResult<FavoriteFolderResponse>> CreateFolder([FromBody] CreateFavoriteFolderRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var folder = new FavoriteFolder
        {
            FolderName = request.FolderName.Trim(),
            CreateTime = DateTime.Now,
            UserID = userId
        };

        _db.FavoriteFolders.Add(folder);
        await _db.SaveChangesAsync();

        return Ok(new FavoriteFolderResponse
        {
            FolderID = folder.FolderID,
            FolderName = folder.FolderName ?? "",
            CreateTime = folder.CreateTime,
            PostCount = 0
        });
    }

    [HttpPut("{folderId}")]
    public async Task<ActionResult<FavoriteFolderResponse>> UpdateFolder(int folderId, [FromBody] CreateFavoriteFolderRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var folder = await _db.FavoriteFolders.FirstOrDefaultAsync(f => f.FolderID == folderId && f.UserID == userId);
        if (folder == null)
            return NotFound(new { message = "收藏夹不存在" });

        folder.FolderName = request.FolderName.Trim();
        await _db.SaveChangesAsync();

        return Ok(new FavoriteFolderResponse
        {
            FolderID = folder.FolderID,
            FolderName = folder.FolderName ?? "",
            CreateTime = folder.CreateTime,
            PostCount = await _db.FolderPosts.CountAsync(fp => fp.FolderID == folder.FolderID)
        });
    }

    [HttpDelete("{folderId}")]
    public async Task<ActionResult> DeleteFolder(int folderId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var folder = await _db.FavoriteFolders.FirstOrDefaultAsync(f => f.FolderID == folderId && f.UserID == userId);
        if (folder == null)
            return NotFound(new { message = "收藏夹不存在" });

        var folderPosts = await _db.FolderPosts.Where(fp => fp.FolderID == folderId).ToListAsync();
        _db.FolderPosts.RemoveRange(folderPosts);
        _db.FavoriteFolders.Remove(folder);
        await _db.SaveChangesAsync();

        return Ok(new { message = "收藏夹已删除" });
    }

    [HttpPost("{folderId}/posts/{postId}")]
    public async Task<ActionResult> AddPost(int folderId, int postId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var folder = await _db.FavoriteFolders.FirstOrDefaultAsync(f => f.FolderID == folderId && f.UserID == userId);
        if (folder == null)
            return NotFound(new { message = "收藏夹不存在" });

        var postExists = await _db.Posts.CountAsync(p => p.PostID == postId && p.Status != "Deleted") > 0;
        if (!postExists)
            return NotFound(new { message = "帖子不存在" });

        var exists = await _db.FolderPosts.CountAsync(fp => fp.FolderID == folderId && fp.PostID == postId) > 0;
        if (!exists)
        {
            _db.FolderPosts.Add(new FolderPost { FolderID = folderId, PostID = postId });
            await _db.SaveChangesAsync();
        }

        return Ok(new { message = "已收藏" });
    }

    [HttpGet("{folderId}/posts")]
    public async Task<ActionResult<List<PostListItemResponse>>> GetFolderPosts(int folderId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var folder = await _db.FavoriteFolders.FirstOrDefaultAsync(f => f.FolderID == folderId && f.UserID == userId);
        if (folder == null)
            return NotFound(new { message = "收藏夹不存在" });

        var posts = await _db.FolderPosts
            .Include(fp => fp.Post)
            .ThenInclude(p => p!.User)
            .Include(fp => fp.Post)
            .ThenInclude(p => p!.Forum)
            .Where(fp => fp.FolderID == folderId && fp.Post != null && fp.Post.Status != "Deleted")
            .Select(fp => fp.Post!)
            .ToListAsync();

        var postIds = posts.Select(p => p.PostID).ToList();
        var tagRows = await _db.TagPosts
            .Include(tp => tp.Tag)
            .Where(tp => postIds.Contains(tp.PostID))
            .ToListAsync();

        return Ok(posts.Select(p => new PostListItemResponse
        {
            PostID = p.PostID,
            Title = p.Title ?? "",
            ContentPreview = string.IsNullOrWhiteSpace(p.Content)
                ? ""
                : p.Content.Length <= 120 ? p.Content : p.Content[..120] + "...",
            LikeCount = p.LikeCount ?? 0,
            ViewCount = p.ViewCount ?? 0,
            Status = p.Status ?? "",
            CreateTime = p.CreateTime,
            UpdateTime = p.UpdateTime,
            UserID = p.UserID,
            Username = p.User?.Username ?? "",
            ForumID = p.ForumID,
            ForumName = p.Forum?.ForumName ?? "",
            Tags = tagRows
                .Where(t => t.PostID == p.PostID && t.Tag?.TagName != null)
                .Select(t => t.Tag!.TagName!)
                .ToList()
        }).ToList());
    }

    [HttpDelete("{folderId}/posts/{postId}")]
    public async Task<ActionResult> RemovePost(int folderId, int postId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var folder = await _db.FavoriteFolders.FirstOrDefaultAsync(f => f.FolderID == folderId && f.UserID == userId);
        if (folder == null)
            return NotFound(new { message = "收藏夹不存在" });

        var folderPost = await _db.FolderPosts.FirstOrDefaultAsync(fp => fp.FolderID == folderId && fp.PostID == postId);
        if (folderPost != null)
        {
            _db.FolderPosts.Remove(folderPost);
            await _db.SaveChangesAsync();
        }

        return Ok(new { message = "已取消收藏" });
    }
}
