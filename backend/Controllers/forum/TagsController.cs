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
}
