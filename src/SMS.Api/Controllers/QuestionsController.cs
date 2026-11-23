using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SMS.Api.Dtos;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Persistence;

namespace SMS.Api.Controllers;

/// <summary>Question bank — reusable questions per course.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class QuestionsController : ControllerBase
{
    private readonly SmsDbContext _db;
    public QuestionsController(SmsDbContext db) => _db = db;

    static QuestionDto Map(Question q) =>
        new(q.Id, q.CourseId, q.Type.ToString(), q.Text, q.DefaultMarks, q.OptionsJson, q.CorrectAnswer);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<QuestionDto>>> List([FromQuery] int courseId, CancellationToken ct) =>
        Ok(await _db.Questions.AsNoTracking().Where(q => q.CourseId == courseId)
            .OrderBy(q => q.Id).Select(q => Map(q)).ToListAsync(ct));

    [HttpPost]
    public async Task<ActionResult<QuestionDto>> Create(UpsertQuestionDto dto, CancellationToken ct)
    {
        var q = new Question
        {
            CourseId = dto.CourseId, Text = dto.Text, DefaultMarks = dto.DefaultMarks,
            OptionsJson = dto.OptionsJson, CorrectAnswer = dto.CorrectAnswer,
            Type = Enum.TryParse<QuestionType>(dto.Type, true, out var t) ? t : QuestionType.ShortAnswer
        };
        _db.Questions.Add(q);
        await _db.SaveChangesAsync(ct);
        return Ok(Map(q));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var q = await _db.Questions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (q is null) return NotFound();
        _db.Questions.Remove(q);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
