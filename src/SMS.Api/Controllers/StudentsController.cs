using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SMS.Api.Dtos;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Persistence;

namespace SMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StudentsController : ControllerBase
{
    private readonly SmsDbContext _db;
    public StudentsController(SmsDbContext db) => _db = db;

    static StudentDto Map(Student s) =>
        new(s.Id, s.StudentNumber, s.FirstName, s.LastName, s.Email, s.Status.ToString(), s.ProgrammeId);

    /// <summary>Server-side paged, searchable student list — fast even on large tables.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<StudentDto>>> List(
        [FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page = Math.Max(1, page);
        var query = _db.Students.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(s => s.StudentNumber.Contains(q) || s.FirstName.Contains(q) || s.LastName.Contains(q));

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .Skip((page - 1) * pageSize).Take(pageSize).Select(s => Map(s)).ToListAsync(ct);
        return Ok(new PagedResult<StudentDto>(items, total, page, pageSize));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<StudentDto>> Get(int id, CancellationToken ct)
    {
        var s = await _db.Students.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return s is null ? NotFound() : Ok(Map(s));
    }

    [SMS.Api.Auth.HasPermission("students.write")]
    [HttpPost]
    public async Task<ActionResult<StudentDto>> Create(UpsertStudentDto dto, CancellationToken ct)
    {
        if (await _db.Students.AnyAsync(x => x.StudentNumber == dto.StudentNumber, ct))
            return Conflict($"Student number '{dto.StudentNumber}' already exists.");
        var s = new Student { StudentNumber = dto.StudentNumber };
        Apply(dto, s);
        _db.Students.Add(s);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = s.Id }, Map(s));
    }

    [SMS.Api.Auth.HasPermission("students.write")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<StudentDto>> Update(int id, UpsertStudentDto dto, CancellationToken ct)
    {
        var s = await _db.Students.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return NotFound();
        Apply(dto, s);
        await _db.SaveChangesAsync(ct);
        return Ok(Map(s));
    }

    [SMS.Api.Auth.HasPermission("students.write")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var s = await _db.Students.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return NotFound();
        _db.Students.Remove(s);          // becomes a soft-delete in SaveChangesAsync
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    static void Apply(UpsertStudentDto dto, Student s)
    {
        s.FirstName = dto.FirstName;
        s.LastName = dto.LastName;
        s.Email = dto.Email;
        s.ProgrammeId = dto.ProgrammeId;
        s.Status = Enum.TryParse<StudentStatus>(dto.Status, true, out var st) ? st : StudentStatus.Applicant;
        s.DateOfBirth = DateOnly.TryParse(dto.DateOfBirth, out var d) ? d : null;
    }
}
