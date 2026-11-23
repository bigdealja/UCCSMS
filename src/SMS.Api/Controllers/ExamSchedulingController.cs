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
[Route("api/examscheduling")]
[Authorize]
public class ExamSchedulingController : ControllerBase
{
    private readonly SmsDbContext _db;
    private readonly ExamSchedulingService _svc;
    public ExamSchedulingController(SmsDbContext db, ExamSchedulingService svc) { _db = db; _svc = svc; }

    // Rooms
    [HttpGet("rooms")]
    public async Task<ActionResult<IEnumerable<RoomDto>>> Rooms(CancellationToken ct) =>
        Ok(await _db.Rooms.AsNoTracking().Where(r => r.IsActive).OrderBy(r => r.Code)
            .Select(r => new RoomDto(r.Id, r.Code, r.Name, r.CampusId, r.Capacity)).ToListAsync(ct));

    [HttpPost("rooms")]
    public async Task<ActionResult<RoomDto>> CreateRoom(UpsertRoomDto dto, CancellationToken ct)
    {
        var r = new Room { Code = dto.Code, Name = dto.Name, CampusId = dto.CampusId, Capacity = dto.Capacity };
        _db.Rooms.Add(r); await _db.SaveChangesAsync(ct);
        return Ok(new RoomDto(r.Id, r.Code, r.Name, r.CampusId, r.Capacity));
    }

    [HttpPost("exams/{examId:int}/allocate-room")]
    public async Task<ActionResult<object>> Allocate(int examId, AllocateRoomDto dto, CancellationToken ct)
    {
        try { var a = await _svc.AllocateRoomAsync(examId, dto.RoomId, dto.Seats, ct);
              return Ok(new { a.Id, a.RoomId, a.SeatsAllocated }); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("exams/{examId:int}/invigilators")]
    public async Task<IActionResult> Invigilator(int examId, AssignInvigilatorDto dto, CancellationToken ct)
    {
        var role = Enum.TryParse<InvigilatorRole>(dto.Role, true, out var r) ? r : InvigilatorRole.Assistant;
        await _svc.AssignInvigilatorAsync(examId, dto.StaffFacultyId, role, ct);
        return NoContent();
    }

    // Special arrangements
    [HttpGet("students/{studentId:int}/arrangements")]
    public async Task<ActionResult<IEnumerable<ArrangementDto>>> Arrangements(int studentId, CancellationToken ct) =>
        Ok(await _db.SpecialArrangements.AsNoTracking().Where(a => a.StudentId == studentId)
            .Select(a => new ArrangementDto(a.Id, a.StudentId, a.Type.ToString(), a.ExtraTimePercent, a.Details, a.Approved)).ToListAsync(ct));

    [HttpPost("arrangements")]
    public async Task<ActionResult<ArrangementDto>> AddArrangement(UpsertArrangementDto dto, CancellationToken ct)
    {
        var type = Enum.TryParse<SpecialArrangementType>(dto.Type, true, out var t) ? t : SpecialArrangementType.Other;
        var a = await _svc.AddArrangementAsync(new SpecialArrangement {
            StudentId = dto.StudentId, Type = type, ExtraTimePercent = dto.ExtraTimePercent,
            Details = dto.Details, Approved = dto.Approved, ApprovedBy = User.Identity?.Name }, ct);
        return Ok(new ArrangementDto(a.Id, a.StudentId, a.Type.ToString(), a.ExtraTimePercent, a.Details, a.Approved));
    }

    // Clash detection
    [HttpGet("clashes/{termId:int}")]
    public async Task<ActionResult<IEnumerable<Clash>>> Clashes(int termId, CancellationToken ct)
        => Ok(await _svc.DetectClashesAsync(termId, ct));
}
