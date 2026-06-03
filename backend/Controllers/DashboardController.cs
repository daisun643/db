using Backend.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "Dashboard")]
public class DashboardController : ControllerBase
{
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(ILogger<DashboardController> logger)
    {
        _logger = logger;
    }

    [HttpGet("info")]
    public IActionResult GetDashboardInfo()
    {
        var userEmail = User.Claims.FirstOrDefault(c => c.Type == "Email")?.Value ?? "Unknown";
        var userRoles = User.Claims.Where(c => c.Type == "Role").Select(c => c.Value).ToList();

        _logger.LogInformation("用户 {Email} 访问仪表板", userEmail);

        return Ok(new
        {
            message = "欢迎来到管理后台",
            userEmail = userEmail,
            roles = userRoles,
            permissions = User.Claims.Where(c => c.Type == "Permission").Select(c => c.Value).ToList()
        });
    }

    [HttpGet("stats")]
    public IActionResult GetDashboardStats()
    {
        // 这里可以返回一些统计信息
        return Ok(new
        {
            totalUsers = 0,
            totalPosts = 0,
            activeUsers = 0,
            pendingReviews = 0
        });
    }
}
