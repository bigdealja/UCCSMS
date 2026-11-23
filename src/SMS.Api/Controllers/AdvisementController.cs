using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMS.Api.Auth;
using SMS.Infrastructure.Advisement;

namespace SMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[HasPermission("students.read")]
public class AdvisementController : ControllerBase
{
    private readonly AdvisementService _svc;
    public AdvisementController(AdvisementService svc) => _svc = svc;

    [HttpGet("students/{studentId:int}/progress")]
    public async Task<ActionResult<StudentProgress>> Progress(int studentId, CancellationToken ct)
    { try { return Ok(await _svc.GetProgressAsync(studentId, ct)); } catch (InvalidOperationException ex) { return NotFound(ex.Message); } }

    [HttpGet("students/{studentId:int}/graduation-check")]
    public async Task<ActionResult<GraduationVerdict>> Graduation(int studentId, CancellationToken ct)
    { try { return Ok(await _svc.GraduationCheckAsync(studentId, ct)); } catch (InvalidOperationException ex) { return NotFound(ex.Message); } }
}
