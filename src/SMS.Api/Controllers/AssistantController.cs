using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SMS.Api.Dtos;
using SMS.Domain.Entities;
using SMS.Infrastructure.Assistant;
using SMS.Infrastructure.Persistence;

namespace SMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AssistantController : ControllerBase
{
    private readonly SmsDbContext _db;
    private readonly AssistantService _svc;
    public AssistantController(SmsDbContext db, AssistantService svc) { _db = db; _svc = svc; }

    /// <summary>Ask a grounded question. Pass studentId to include that student's own record facts.
    /// NOTE: in production, RBAC must ensure a student can only pass their own id.</summary>
    [HttpPost("ask")]
    public async Task<ActionResult<object>> Ask(AskDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Question)) return BadRequest("A question is required.");
        var a = await _svc.AskAsync(dto.Question, dto.StudentId, User.Identity?.Name, ct);
        return Ok(new { a.Answer, a.Sources, a.UsedAi });
    }

    // Knowledge base management
    [HttpGet("articles")]
    public async Task<ActionResult<IEnumerable<KnowledgeArticleDto>>> Articles([FromQuery] string? category, CancellationToken ct)
    {
        var q = _db.KnowledgeArticles.AsNoTracking().Where(a => a.IsPublished);
        if (!string.IsNullOrWhiteSpace(category)) q = q.Where(a => a.Category == category);
        return Ok(await q.OrderBy(a => a.Category).ThenBy(a => a.Title)
            .Select(a => new KnowledgeArticleDto(a.Id, a.Title, a.Category, a.Body, a.Tags)).ToListAsync(ct));
    }

    [HttpPost("articles")]
    public async Task<ActionResult<KnowledgeArticleDto>> CreateArticle(UpsertArticleDto dto, CancellationToken ct)
    {
        var a = new KnowledgeArticle { Title = dto.Title, Category = dto.Category, Body = dto.Body, Tags = dto.Tags };
        _db.KnowledgeArticles.Add(a); await _db.SaveChangesAsync(ct);
        return Ok(new KnowledgeArticleDto(a.Id, a.Title, a.Category, a.Body, a.Tags));
    }
}
