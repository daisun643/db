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
public class ForumsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ForumsController(AppDbContext db) => _db = db;

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<ForumSummaryResponse>>> GetAll()
    {
        var forums = await _db.Forums
            .Include(f => f.ForumManagers)
            .ThenInclude(fm => fm.User)
            .ToListAsync();

        return Ok(forums.Select(MapForum).ToList());
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<ForumSummaryResponse>> GetById(int id)
    {
        var forum = await _db.Forums
            .Include(f => f.ForumManagers)
            .ThenInclude(fm => fm.User)
            .FirstOrDefaultAsync(f => f.ForumID == id);

        return forum is null ? NotFound() : Ok(MapForum(forum));
    }

    [HttpPost]
    [RequirePermission("forums.create")]
    public async Task<ActionResult<ForumSummaryResponse>> Create([FromBody] CreateForumRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var forum = new Forum
        {
            ForumName = request.ForumName.Trim(),
            Description = request.Description,
            CreatorID = userId,
            CreateTime = DateTime.Now,
            Status = "Active"
        };

        _db.Forums.Add(forum);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = forum.ForumID }, MapForum(forum));
    }

    [HttpPut("{id}")]
    [RequirePermission("forums.edit")]
    public async Task<ActionResult<ForumSummaryResponse>> Update(int id, [FromBody] UpdateForumRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var forum = await _db.Forums.FindAsync(id);
        if (forum == null)
            return NotFound();

        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var canManage = User.IsInRole("Admin") ||
            await _db.ForumManagers.CountAsync(fm => fm.ForumID == id && fm.UserID == userId) > 0;
        if (!canManage)
            return Forbid();

        forum.ForumName = request.ForumName.Trim();
        forum.Description = request.Description;
        forum.Status = request.Status is "Active" or "Inactive" ? request.Status : "Active";

        await _db.SaveChangesAsync();
        return Ok(MapForum(forum));
    }

    [HttpDelete("{id}")]
    [RequirePermission("forums.delete")]
    public async Task<ActionResult> Delete(int id)
    {
        var forum = await _db.Forums.FindAsync(id);
        if (forum == null)
            return NotFound();

        _db.Forums.Remove(forum);
        await _db.SaveChangesAsync();
        return Ok(new { message = "删除成功" });
    }

    [HttpPost("{id}/managers")]
    [RequirePermission("forums.edit")]
    public async Task<ActionResult> AddManager(int id, [FromBody] AssignForumManagerRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var forumExists = await _db.Forums.CountAsync(f => f.ForumID == id) > 0;
        if (!forumExists)
            return NotFound(new { message = "论坛不存在" });

        var userExists = await _db.Users.CountAsync(u => u.UserID == request.UserID && u.Status == "Active") > 0;
        if (!userExists)
            return BadRequest(new { message = "用户不存在或不可用" });

        var exists = await _db.ForumManagers.CountAsync(fm => fm.ForumID == id && fm.UserID == request.UserID) > 0;
        if (!exists)
        {
            _db.ForumManagers.Add(new ForumManager { ForumID = id, UserID = request.UserID });
            await _db.SaveChangesAsync();
        }

        await CreateNotificationAsync(request.UserID, "版主权限已分配", $"你已成为论坛 #{id} 的版主");
        await _db.SaveChangesAsync();
        return Ok(new { message = "版主已指派" });
    }

    [HttpDelete("{id}/managers/{userId}")]
    [RequirePermission("forums.edit")]
    public async Task<ActionResult> RemoveManager(int id, int userId)
    {
        var manager = await _db.ForumManagers.FirstOrDefaultAsync(fm => fm.ForumID == id && fm.UserID == userId);
        if (manager == null)
            return NotFound();

        _db.ForumManagers.Remove(manager);
        await CreateNotificationAsync(userId, "版主权限已移除", $"你不再是论坛 #{id} 的版主");
        await _db.SaveChangesAsync();
        return Ok(new { message = "版主已移除" });
    }

    private ForumSummaryResponse MapForum(Forum forum)
    {
        return new ForumSummaryResponse
        {
            ForumID = forum.ForumID,
            ForumName = forum.ForumName ?? "",
            Description = forum.Description ?? "",
            Status = forum.Status ?? "",
            CreateTime = forum.CreateTime,
            PostCount = _db.Posts.Count(p => p.ForumID == forum.ForumID && p.Status != "Deleted"),
            Managers = forum.ForumManagers
                .Where(fm => fm.User != null)
                .Select(fm => new ForumManagerResponse
                {
                    UserID = fm.UserID,
                    Username = fm.User!.Username ?? "",
                    Email = fm.User.Email ?? ""
                })
                .ToList()
        };
    }

    private async Task CreateNotificationAsync(int userId, string title, string content)
    {
        _db.Notifications.Add(new Notification
        {
            UserID = userId,
            Title = title,
            Content = content,
            CreateTime = DateTime.Now
        });
        await Task.CompletedTask;
    }
}
