using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SMS.Api.Dtos;
using SMS.Infrastructure.Persistence;

namespace SMS.Api.Controllers;

/// <summary>Read-only reference data the UI needs (programmes, terms, courses) to drive
/// the student form and the grade-import screen.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LookupsController : ControllerBase
{
    private readonly SmsDbContext _db;
    public LookupsController(SmsDbContext db) => _db = db;

    [HttpGet("programmes")]
    public async Task<ActionResult<IEnumerable<LookupDto>>> Programmes(CancellationToken ct) =>
        Ok(await _db.Programmes.AsNoTracking().OrderBy(p => p.Name)
            .Select(p => new LookupDto(p.Id, p.Code, p.Name)).ToListAsync(ct));

    [HttpGet("terms")]
    public async Task<ActionResult<IEnumerable<LookupDto>>> Terms(CancellationToken ct) =>
        Ok(await _db.Terms.AsNoTracking().OrderByDescending(t => t.Year).ThenBy(t => t.Name)
            .Select(t => new LookupDto(t.Id, t.Year.ToString(), t.Name + " " + t.Year)).ToListAsync(ct));

    [HttpGet("courses")]
    public async Task<ActionResult<IEnumerable<LookupDto>>> Courses([FromQuery] int? programmeId, CancellationToken ct)
    {
        var q = _db.Courses.AsNoTracking();
        if (programmeId is int pid) q = q.Where(c => c.ProgrammeId == pid);
        return Ok(await q.OrderBy(c => c.Code)
            .Select(c => new LookupDto(c.Id, c.Code, c.Title)).ToListAsync(ct));
    }
}
