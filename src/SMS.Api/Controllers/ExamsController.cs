using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SMS.Api.Dtos;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Exams;
using SMS.Infrastructure.Persistence;

namespace SMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ExamsController : ControllerBase
{
    private readonly SmsDbContext _db;
    private readonly ExamService _exams;
    public ExamsController(SmsDbContext db, ExamService exams) { _db = db; _exams = exams; }

    static ExamDto Map(Exam e) => new(e.Id, e.Title, e.CourseId, e.TermId,
        e.ScheduledStart?.ToString("o"), e.DurationMinutes, e.Room, e.TotalMarks, e.Status.ToString(),
        e.Questions?.Count ?? 0);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ExamDto>>> List([FromQuery] int? courseId, [FromQuery] int? termId, CancellationToken ct)
    {
        var q = _db.Exams.Include(e => e.Questions).AsNoTracking().AsQueryable();
        if (courseId is int c) q = q.Where(e => e.CourseId == c);
        if (termId is int t) q = q.Where(e => e.TermId == t);
        return Ok(await q.OrderByDescending(e => e.ScheduledStart).Select(e => Map(e)).ToListAsync(ct));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ExamDto>> Get(int id, CancellationToken ct)
    {
        var e = await _db.Exams.Include(x => x.Questions).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return e is null ? NotFound() : Ok(Map(e));
    }

    [HttpPost]
    public async Task<ActionResult<ExamDto>> Create(UpsertExamDto dto, CancellationToken ct)
    {
        var e = new Exam
        {
            Title = dto.Title, CourseId = dto.CourseId, TermId = dto.TermId,
            DurationMinutes = dto.DurationMinutes, Room = dto.Room,
            ScheduledStart = DateTimeOffset.TryParse(dto.ScheduledStart, out var d) ? d : null,
            Status = ExamStatus.Scheduled
        };
        _db.Exams.Add(e);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = e.Id }, Map(e));
    }

    [HttpPost("{id:int}/questions")]
    public async Task<ActionResult<object>> AddQuestion(int id, AddExamQuestionDto dto, CancellationToken ct)
    {
        try { var eq = await _exams.AddQuestionAsync(id, dto.QuestionId, dto.Marks, ct);
              return Ok(new { eq.Id, eq.QuestionId, eq.Order, eq.Marks }); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("{id:int}/results")]
    public async Task<IActionResult> RecordResult(int id, RecordResultDto dto, CancellationToken ct)
    {
        await _exams.RecordResultAsync(id, dto.StudentId, dto.Score, ct);
        return NoContent();
    }

    /// <summary>Post all recorded results into the gradebook as one undoable batch.</summary>
    [HttpPost("{id:int}/post-to-gradebook")]
    public async Task<ActionResult<PostResultsOutcome>> Post(int id, CancellationToken ct)
    {
        try { return Ok(await _exams.PostResultsToGradebookAsync(id, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }
}
