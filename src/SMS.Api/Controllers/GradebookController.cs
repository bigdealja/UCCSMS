using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SMS.Api.Auth;
using SMS.Api.Dtos;
using SMS.Domain.Enums;
using SMS.Infrastructure.Grading;
using SMS.Infrastructure.Persistence;

namespace SMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GradebookController : ControllerBase
{
    private readonly SmsDbContext _db;
    private readonly GradebookService _svc;
    public GradebookController(SmsDbContext db, GradebookService svc) { _db = db; _svc = svc; }

    [HttpGet("components")]
    [HasPermission("grades.read")]
    public async Task<ActionResult<object>> Components([FromQuery] int courseId, [FromQuery] int termId, CancellationToken ct)
    {
        var comps = await _db.AssessmentComponents.AsNoTracking().Where(c => c.CourseId == courseId && c.TermId == termId).OrderBy(c => c.Id)
            .Select(c => new ComponentDto(c.Id, c.Name, c.Category.ToString(), c.Weight, c.MaxMark)).ToListAsync(ct);
        return Ok(new { components = comps, weightTotal = comps.Sum(c => c.Weight) });
    }

    public record AddComponentBody(int CourseId, int TermId, string Name, string Category, double Weight, double MaxMark);
    [HttpPost("components")]
    [HasPermission("grades.import")]
    public async Task<ActionResult<object>> AddComponent(AddComponentBody b, CancellationToken ct)
    {
        var cat = Enum.TryParse<GradeCategory>(b.Category, true, out var c) ? c : GradeCategory.Coursework;
        try { var comp = await _svc.AddComponentAsync(b.CourseId, b.TermId, b.Name, cat, b.Weight, b.MaxMark <= 0 ? 100 : b.MaxMark, ct);
              return Ok(new { comp.Id }); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    public record MarkBody(int ComponentId, int StudentId, double Mark);
    [HttpPost("marks")]
    [HasPermission("grades.import")]
    public async Task<IActionResult> EnterMark(MarkBody b, CancellationToken ct)
    { try { await _svc.EnterMarkAsync(b.ComponentId, b.StudentId, b.Mark, User.Identity?.Name ?? "lecturer", "Manual", ct); return NoContent(); }
      catch (InvalidOperationException ex) { return BadRequest(ex.Message); } }

    /// <summary>The in-system grade sheet: each registered student with their component marks and the
    /// computed coursework%, final% and letter — the replacement for the external spreadsheet.</summary>
    [HttpGet("sheet")]
    [HasPermission("grades.read")]
    public async Task<ActionResult<object>> Sheet([FromQuery] int courseId, [FromQuery] int termId, CancellationToken ct)
    {
        var comps = await _db.AssessmentComponents.AsNoTracking().Where(c => c.CourseId == courseId && c.TermId == termId).OrderBy(c => c.Id).ToListAsync(ct);
        var students = await _db.Enrollments.AsNoTracking().Where(e => e.CourseId == courseId && e.TermId == termId && e.Status == EnrollmentStatus.Registered)
            .Select(e => e.Student).ToListAsync(ct);
        var marks = (await _db.ComponentMarks.AsNoTracking().Where(m => comps.Select(c => c.Id).Contains(m.AssessmentComponentId)).ToListAsync(ct))
            .GroupBy(m => m.StudentId).ToDictionary(g => g.Key, g => g.ToDictionary(x => x.AssessmentComponentId, x => x.Mark));

        var rows = new List<object>();
        foreach (var s in students)
        {
            ComputedGrade? g = null;
            try { g = await _svc.ComputeAsync(courseId, termId, s.Id, ct); } catch { }
            rows.Add(new {
                studentId = s.Id, s.StudentNumber, name = $"{s.LastName}, {s.FirstName}",
                marks = comps.ToDictionary(c => c.Id.ToString(), c => marks.TryGetValue(s.Id, out var mm) && mm.TryGetValue(c.Id, out var v) ? (double?)v : null),
                coursework = g?.CourseworkPct, final = g?.FinalPct, letter = g?.Letter, passed = g?.Passed
            });
        }
        return Ok(new { components = comps.Select(c => new ComponentDto(c.Id, c.Name, c.Category.ToString(), c.Weight, c.MaxMark)), rows });
    }

    [HttpGet("compute")]
    [HasPermission("grades.read")]
    public async Task<ActionResult<ComputedGrade>> Compute([FromQuery] int courseId, [FromQuery] int termId, [FromQuery] int studentId, CancellationToken ct)
    { try { return Ok(await _svc.ComputeAsync(courseId, termId, studentId, ct)); }
      catch (InvalidOperationException ex) { return BadRequest(ex.Message); } }

    public record PostBody(int CourseId, int TermId);
    [HttpPost("post")]
    [HasPermission("grades.import")]
    public async Task<ActionResult<PostResult>> Post(PostBody b, CancellationToken ct)
    { try { return Ok(await _svc.PostAsync(b.CourseId, b.TermId, ct)); }
      catch (InvalidOperationException ex) { return BadRequest(ex.Message); } }
}
