using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SMS.Api.Dtos;
using SMS.Infrastructure.Persistence;

namespace SMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EnrollmentsController : ControllerBase
{
    private readonly SmsDbContext _db;
    public EnrollmentsController(SmsDbContext db) => _db = db;

    [HttpGet("by-term/{termId:int}")]
    public async Task<ActionResult<IEnumerable<EnrollmentDto>>> ByTerm(int termId, CancellationToken ct)
    {
        var items = await _db.Enrollments.AsNoTracking().Where(e => e.TermId == termId)
            .Select(e => new EnrollmentDto(e.Id, e.StudentId, e.CourseId, e.TermId, e.Status.ToString(),
                e.LetterGrade, e.CourseworkMark, e.FinalExamMark, e.ImportBatchId, e.GradeSource))
            .ToListAsync(ct);
        return Ok(items);
    }

    /// <summary>Undo an entire grade-import batch in one call — the capability the legacy
    /// tool tracked (batch GUID) but never exposed. Soft-reverts grades for the batch.</summary>
    [HttpPost("undo-batch/{batchId:guid}")]
    public async Task<ActionResult<object>> UndoBatch(Guid batchId, CancellationToken ct)
    {
        var affected = await _db.Enrollments.Where(e => e.ImportBatchId == batchId).ToListAsync(ct);
        foreach (var e in affected)
        {
            e.LetterGrade = null; e.CourseworkMark = null; e.FinalExamMark = null;
            e.GradeSource = null; e.GradedUtc = null; e.ImportBatchId = null;
        }
        await _db.SaveChangesAsync(ct);
        return Ok(new { batchId, reverted = affected.Count });
    }
}
