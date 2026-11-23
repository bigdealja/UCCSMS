using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMS.Api.Auth;
using SMS.Domain.Enums;
using SMS.Infrastructure.AssessmentOps;

namespace SMS.Api.Controllers;

[ApiController]
[Route("api/assessmentops")]
[Authorize]
[HasPermission("exams.manage")]
public class AssessmentOpsController : ControllerBase
{
    private readonly AssessmentOpsService _svc;
    public AssessmentOpsController(AssessmentOpsService svc) => _svc = svc;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SittingStatus>>> List([FromQuery] int? termId, CancellationToken ct)
        => Ok(await _svc.ListAsync(termId, needsAttentionOnly: false, ct));

    [HttpGet("needs-attention")]
    public async Task<ActionResult<IEnumerable<SittingStatus>>> NeedsAttention([FromQuery] int? termId, CancellationToken ct)
        => Ok(await _svc.ListAsync(termId, needsAttentionOnly: true, ct));

    public record CreateBody(int ExamId, string DueDate);
    [HttpPost]
    public async Task<ActionResult<object>> Create(CreateBody b, CancellationToken ct)
    {
        if (!DateOnly.TryParse(b.DueDate, out var d)) return BadRequest("Invalid due date.");
        try { var r = await _svc.CreateAsync(b.ExamId, d, ct); return Ok(new { r.Id }); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    public record MilestoneBody(string Milestone);
    [HttpPost("{id:int}/milestone")]
    public async Task<IActionResult> Milestone(int id, MilestoneBody b, CancellationToken ct)
    {
        if (!Enum.TryParse<MilestoneName>(b.Milestone, true, out var m)) return BadRequest("Invalid milestone.");
        try { await _svc.RecordMilestoneAsync(id, m, ct); return NoContent(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    public record ExceptionBody(string? Note);
    [HttpPost("{id:int}/exception")]
    public async Task<IActionResult> Exception(int id, ExceptionBody b, CancellationToken ct)
    { try { await _svc.SetExceptionAsync(id, b.Note, ct); return NoContent(); } catch (InvalidOperationException ex) { return BadRequest(ex.Message); } }

    public record LateBody(string Reason);
    [HttpPost("{id:int}/late-reason")]
    public async Task<IActionResult> Late(int id, LateBody b, CancellationToken ct)
    { try { await _svc.SetLateReasonAsync(id, b.Reason, ct); return NoContent(); } catch (InvalidOperationException ex) { return BadRequest(ex.Message); } }
}
