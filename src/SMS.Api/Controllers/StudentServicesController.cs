using Microsoft.AspNetCore.Mvc;
using SMS.Api.Auth;
using SMS.Infrastructure.StudentServices;

namespace SMS.Api.Controllers;

[ApiController]
[Route("api/student-services")]
public class StudentServicesController : ControllerBase
{
    private readonly StudentServicesService _svc;
    public StudentServicesController(StudentServicesService svc) => _svc = svc;

    public record AssignHousingDto(int StudentId, string Hall, string Room, int? TermId);
    public record SportDto(int StudentId, string Sport, string? Team, string? Role, string Season);
    public record ChapelDto(int StudentId, string EventName, DateOnly EventDate, bool Present);

    [HttpPost("housing"), HasPermission("studentservices.manage")]
    public async Task<IActionResult> Assign(AssignHousingDto d, CancellationToken ct)
        => Ok(await _svc.AssignHousingAsync(d.StudentId, d.Hall, d.Room, d.TermId, ct));

    [HttpPost("housing/{id:int}/check-in"), HasPermission("studentservices.manage")]
    public async Task<IActionResult> CheckIn(int id, CancellationToken ct) { await _svc.CheckInAsync(id, ct); return NoContent(); }

    [HttpPost("housing/{id:int}/check-out"), HasPermission("studentservices.manage")]
    public async Task<IActionResult> CheckOut(int id, CancellationToken ct) { await _svc.CheckOutAsync(id, ct); return NoContent(); }

    [HttpPost("sports"), HasPermission("studentservices.manage")]
    public async Task<IActionResult> AddSport(SportDto d, CancellationToken ct)
        => Ok(await _svc.AddSportAsync(d.StudentId, d.Sport, d.Team, d.Role, d.Season, ct));

    [HttpPost("chapel"), HasPermission("studentservices.manage")]
    public async Task<IActionResult> Chapel(ChapelDto d, CancellationToken ct)
        { await _svc.RecordChapelAsync(d.StudentId, d.EventName, d.EventDate, d.Present, ct); return NoContent(); }

    [HttpGet("chapel/{studentId:int}/summary"), HasPermission("studentservices.read")]
    public async Task<IActionResult> ChapelSummary(int studentId, CancellationToken ct)
        => Ok(await _svc.ChapelSummaryAsync(studentId, ct));
}

/// <summary>Confidential health records — separate controller gated by its own permission;
/// every read/write is captured by the audit layer.</summary>
[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    private readonly StudentServicesService _svc;
    public HealthController(StudentServicesService svc) => _svc = svc;

    public record VisitDto(int StudentId, string Reason, string? Notes, string? SeenBy);

    [HttpPost("visits"), HasPermission("health.manage")]
    public async Task<IActionResult> Record(VisitDto d, CancellationToken ct)
        => Ok(await _svc.RecordVisitAsync(d.StudentId, d.Reason, d.Notes, d.SeenBy, ct));
}
