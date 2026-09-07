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
    private readonly INotificationService _notificationService;

    public FriendsController(AppDbContext db, INotificationService notificationService)
    {
        _db = db;
        _notificationService = notificationService;
    }

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

    [HttpGet("sent")]
    public async Task<ActionResult<List<FriendResponse>>> GetSentRequests()
    {
        var userId = CurrentUserId();
        var requests = await _db.FriendShips
            .Include(f => f.Friend)
            .Where(f => f.UserID == userId)
            .OrderByDescending(f => f.UpdateTime)
            .Take(20)
            .ToListAsync();

        return Ok(requests.Select(f => MapFriend(f, userId)).ToList());
    }
    [HttpGet("relation/{userId}")]
    public async Task<ActionResult> GetRelation(int userId)
    {
        var currentUserId = CurrentUserId();
        if (userId == currentUserId)
            return Ok(new { userId, relation = "self", friendshipId = 0 });

        var friendship = await _db.FriendShips.FirstOrDefaultAsync(f =>
            (f.UserID == currentUserId && f.FriendID == userId) ||
            (f.UserID == userId && f.FriendID == currentUserId));

        var relation = "none";
        if (friendship != null)
        {
            if (friendship.Status == "Accepted")
                relation = "friend";
            else if (friendship.Status == "Pending")
                relation = friendship.UserID == currentUserId ? "pending-sent" : "pending-received";
        }

        return Ok(new
        {
            userId,
            relation,
            friendshipId = friendship?.FriendshipID ?? 0
        });
    }

    [HttpPost]
    public async Task<ActionResult<FriendResponse>> CreateRequest([FromBody] CreateFriendRequest request)
    {
        var userId = CurrentUserId();
        if (!request.UserID.HasValue && string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { message = "请提供用户ID或邮箱" });

        var email = request.Email?.Trim();

        var target = request.UserID.HasValue
            ? await _db.Users.FindAsync(request.UserID.Value)
            : await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (target == null)
            return NotFound(new { message = "用户不存在" });
        if (target.UserID == userId)
            return BadRequest(new { message = "不能添加自己为好友" });
        if (!string.Equals(target.Status, "Active", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "该用户当前不可添加" });

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
                // updateTime 由 TRG_FriendShip_TouchTime 统一刷新
                await _db.SaveChangesAsync();
                await CreateNotificationAsync(target.UserID, "新的好友申请", "有人请求添加你为好友", existing.FriendshipID, "request");
                await _db.SaveChangesAsync();
                await _db.Entry(existing).ReloadAsync();
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
        await _db.SaveChangesAsync();
        await CreateNotificationAsync(target.UserID, "新的好友申请", "有人请求添加你为好友", friendship.FriendshipID, "request");
        await _db.SaveChangesAsync();

        await _db.Entry(friendship).ReloadAsync();
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

        if (friendship.Status != "Pending")
            return Conflict(new { message = "该好友申请已经处理" });
        friendship.Status = "Accepted";
        await CreateNotificationAsync(friendship.UserID, "好友申请已通过", "你的好友申请已被接受", id, "accepted");
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

        if (friendship.Status != "Pending")
            return Conflict(new { message = "该好友申请已经处理" });
        friendship.Status = "Rejected";
        await CreateNotificationAsync(friendship.UserID, "好友申请已拒绝", "你的好友申请已被拒绝", id, "rejected");
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
        await CreateNotificationAsync(notifyUserId, "好友关系已解除", "有用户与你解除了好友关系", id, "deleted");
        await _db.SaveChangesAsync();
        return Ok(new { message = "好友关系已删除" });
    }

    private int CurrentUserId() => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

    private async Task CreateNotificationAsync(int userId, string title, string content, int friendshipId, string action)
    {
        await _notificationService.CreateAsync(new CreateNotificationOptions
        {
            UserID = userId,
            Type = "Friend",
            Title = title,
            Content = content,
            TargetType = "Friendship",
            TargetID = friendshipId,
            Link = "/messages",
            EventKey = $"friend:{friendshipId}:{userId}:{action}"
        });
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
            Status = friendship.Status,
            CreateTime = friendship.CreateTime,
            UpdateTime = friendship.UpdateTime
        };
    }
}
