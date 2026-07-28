using Backend.Data;
using Backend.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TagsController : ControllerBase
{
    private readonly AppDbContext _db;

    public TagsController(AppDbContext db) => _db = db;

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<string>>> GetTags([FromQuery] string? keyword)
    {
        var query = _db.PostTags.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var trimmed = keyword.Trim();
            query = query.Where(t => t.TagName != null && t.TagName.Contains(trimmed));
        }

        return Ok(await query
            .OrderBy(t => t.TagName)
            .Select(t => t.TagName ?? "")
            .Where(t => t != "")
            .Take(20)
            .ToListAsync());
    }

    [HttpPost("suggest")]
    [AllowAnonymous]
    public async Task<ActionResult<List<string>>> Suggest([FromBody] TagSuggestRequest request)
    {
        var existingTags = await _db.PostTags
            .Select(t => t.TagName ?? "")
            .Where(t => t != "")
            .ToListAsync();

        var text = $"{request.Title} {request.Content}";
        var suggestions = existingTags
            .Where(tag => text.Contains(tag, StringComparison.OrdinalIgnoreCase))
            .Take(5)
            .ToList();

        if (suggestions.Count == 0)
        {
            suggestions = text
                .Split([' ', ',', '.', '，', '。', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries)
                .Where(word => word.Length >= 2 && word.Length <= 20)
                .GroupBy(word => word, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .Take(5)
                .ToList();
        }

        return Ok(suggestions);
    }

    [HttpGet("stats")]
    [AllowAnonymous]
    public async Task<ActionResult<List<TagStatsResponse>>> GetStats([FromQuery] int top = 10)
    {
        top = Math.Clamp(top, 1, 50);

        var rows = await _db.TagPosts
            .Where(tp => tp.Post != null &&
                         (tp.Post.Status == "Active" || tp.Post.Status == "Elite" || tp.Post.Status == "Pinned") &&
                         tp.Tag != null && tp.Tag.TagName != null && tp.Tag.TagName != string.Empty)
            .GroupBy(tp => new { tp.TagID, tp.Tag!.TagName })
            .Select(g => new TagStatsResponse
            {
                TagID = g.Key.TagID,
                TagName = g.Key.TagName!,
                PostCount = g.Count()
            })
            .OrderByDescending(x => x.PostCount)
            .ThenBy(x => x.TagName)
            .Take(top)
            .ToListAsync();

        return Ok(rows);
    }
}
