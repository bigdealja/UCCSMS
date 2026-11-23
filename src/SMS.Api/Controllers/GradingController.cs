using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SMS.Api.Dtos;
using SMS.Infrastructure.Grading;
using SMS.Infrastructure.Persistence;

namespace SMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GradingController : ControllerBase
{
    private readonly SmsDbContext _db;
    private readonly GradingService _grading;
    public GradingController(SmsDbContext db, GradingService grading) { _db = db; _grading = grading; }

    /// <summary>All configured grade schemes with their bands (the UCC policy scales).</summary>
    [HttpGet("schemes")]
    public async Task<ActionResult<IEnumerable<GradeSchemeDto>>> Schemes(CancellationToken ct)
    {
        var schemes = await _db.GradeSchemes.Include(s => s.Bands).AsNoTracking()
            .OrderBy(s => s.Level).ToListAsync(ct);
        return Ok(schemes.Select(s => new GradeSchemeDto(s.Id, s.Name, s.Level.ToString(),
            s.EffectiveFrom.ToString("yyyy-MM-dd"), s.OverallPassMark, s.CourseworkPassMark, s.EosaPassMark,
            s.Bands.OrderBy(b => b.SortOrder).Select(b => new GradeBandDto(
                b.Letter, b.MinPercent, b.MaxPercent, b.QualityPoints, b.IsPass, b.CountsInGpa, b.IsSpecial)).ToList())));
    }

    /// <summary>Preview the letter grade + GPA points a mark would earn under a programme's scheme,
    /// plus the policy pass check.</summary>
    [HttpGet("evaluate")]
    public async Task<ActionResult<object>> Evaluate([FromQuery] int programmeId,
        [FromQuery] double coursework, [FromQuery] double eosa, [FromQuery] double final, CancellationToken ct)
    {
        var scheme = await _grading.GetSchemeForProgrammeAsync(programmeId, null, ct);
        if (scheme is null) return NotFound("No grade scheme for that programme/level.");
        var grade = GradingService.Grade(scheme, final);
        var pass = GradingService.CheckPass(scheme, coursework, eosa, final);
        return Ok(new { scheme = scheme.Name, grade.Letter, grade.QualityPoints, grade.IsPass, pass.Passed, pass.Reason });
    }

    [HttpGet("students/{studentId:int}/gpa")]
    public async Task<ActionResult<object>> Gpa(int studentId, CancellationToken ct)
        => Ok(new { studentId, gpa = await _grading.ComputeGpaAsync(studentId, ct) });
}
