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
[Route("api/friends")]
[Authorize]
public class FriendsController : ControllerBase
{
    private readonly AppDbContext _db;

    public FriendsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<FriendResponse>>> GetFriends()
    {
        var userId = CurrentUserId();
        var friendships = await _db.FriendShips
            .Include(f => f.User)
            .Include(f => f.Friend)
            .Where(f => (f.UserID == userId || f.FriendID == userId) && f.Status == "Accepted")
            .OrderByDescending(f => f.UpdateTime)
            .ToListAsync();

        return Ok(friendships.Select(f => MapFriend(f, userId)).ToList());
    }

    [HttpGet("requests")]
    public async Task<ActionResult<List<FriendResponse>>> GetRequests()
    {
        var userId = CurrentUserId();
        var requests = await _db.FriendShips
            .Include(f => f.User)
            .Where(f => f.FriendID == userId && f.Status == "Pending")
            .OrderByDescending(f => f.CreateTime)
            .ToListAsync();

        return Ok(requests.Select(f => MapFriend(f, userId)).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<FriendResponse>> CreateRequest([FromBody] CreateFriendRequest request)
    {
        var userId = CurrentUserId();
        var target = request.UserID.HasValue
            ? await _db.Users.FindAsync(request.UserID.Value)
            : await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        if (target == null)
            return NotFound(new { message = "用户不存在" });
        if (target.UserID == userId)
            return BadRequest(new { message = "不能添加自己为好友" });

        var existing = await _db.FriendShips.FirstOrDefaultAsync(f =>
            (f.UserID == userId && f.FriendID == target.UserID) ||
            (f.UserID == target.UserID && f.FriendID == userId));

        if (existing != null)
        {
            if (existing.Status == "Rejected")
            {
                existing.UserID = userId;
                existing.FriendID = target.UserID;
                existing.Status = "Pending";
                existing.UpdateTime = DateTime.Now;
                await CreateNotificationAsync(target.UserID, "新的好友申请", "有人请求添加你为好友");
                await _db.SaveChangesAsync();
                existing.User = await _db.Users.FindAsync(existing.UserID);
                existing.Friend = await _db.Users.FindAsync(existing.FriendID);
                return Ok(MapFriend(existing, userId));
            }

            return BadRequest(new { message = "好友关系或申请已存在" });
        }

        var friendship = new FriendShip
        {
            UserID = userId,
            FriendID = target.UserID,
            Status = "Pending",
            CreateTime = DateTime.Now,
            UpdateTime = DateTime.Now
        };

        _db.FriendShips.Add(friendship);
        await CreateNotificationAsync(target.UserID, "新的好友申请", "有人请求添加你为好友");
        await _db.SaveChangesAsync();

        friendship.User = await _db.Users.FindAsync(userId);
        friendship.Friend = target;
        return Ok(MapFriend(friendship, userId));
    }

    [HttpPost("{id}/accept")]
    public async Task<ActionResult> Accept(int id)
    {
        var friendship = await _db.FriendShips.FindAsync(id);
        if (friendship == null)
            return NotFound();
        if (friendship.FriendID != CurrentUserId())
            return Forbid();

        friendship.Status = "Accepted";
        friendship.UpdateTime = DateTime.Now;
        if (friendship.UserID.HasValue)
            await CreateNotificationAsync(friendship.UserID.Value, "好友申请已通过", "你的好友申请已被接受");
        await _db.SaveChangesAsync();
        return Ok(new { message = "已接受好友申请" });
    }

    [HttpPost("{id}/reject")]
    public async Task<ActionResult> Reject(int id)
    {
        var friendship = await _db.FriendShips.FindAsync(id);
        if (friendship == null)
            return NotFound();
        if (friendship.FriendID != CurrentUserId())
            return Forbid();

        friendship.Status = "Rejected";
        friendship.UpdateTime = DateTime.Now;
        if (friendship.UserID.HasValue)
            await CreateNotificationAsync(friendship.UserID.Value, "好友申请已拒绝", "你的好友申请已被拒绝");
        await _db.SaveChangesAsync();
        return Ok(new { message = "已拒绝好友申请" });
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        var userId = CurrentUserId();
        var friendship = await _db.FriendShips.FindAsync(id);
        if (friendship == null)
            return NotFound();
        if (friendship.UserID != userId && friendship.FriendID != userId)
            return Forbid();

        var notifyUserId = friendship.UserID == userId ? friendship.FriendID : friendship.UserID;
        _db.FriendShips.Remove(friendship);
        if (notifyUserId.HasValue)
            await CreateNotificationAsync(notifyUserId.Value, "好友关系已解除", "有用户与你解除了好友关系");
        await _db.SaveChangesAsync();
        return Ok(new { message = "好友关系已删除" });
    }

    private int CurrentUserId() => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

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

    private static FriendResponse MapFriend(FriendShip friendship, int currentUserId)
    {
        var otherUser = friendship.UserID == currentUserId ? friendship.Friend : friendship.User;
        return new FriendResponse
        {
            FriendshipID = friendship.FriendshipID,
            UserID = otherUser?.UserID ?? 0,
            Username = otherUser?.Username ?? "",
            Email = otherUser?.Email ?? "",
            Status = friendship.Status ?? "",
            CreateTime = friendship.CreateTime,
            UpdateTime = friendship.UpdateTime
        };
    }
}
