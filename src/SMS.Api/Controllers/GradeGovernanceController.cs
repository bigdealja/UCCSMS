using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SMS.Api.Dtos;
using SMS.Domain.Enums;
using SMS.Infrastructure.Grading;
using SMS.Infrastructure.Persistence;

namespace SMS.Api.Controllers;

[ApiController]
[Route("api/gradegovernance")]
[Authorize]
public class GradeGovernanceController : ControllerBase
{
    private readonly SmsDbContext _db;
    private readonly GradeGovernanceService _svc;
    public GradeGovernanceController(SmsDbContext db, GradeGovernanceService svc) { _db = db; _svc = svc; }

    [HttpGet("changes")]
    public async Task<ActionResult<IEnumerable<GradeChangeDto>>> Changes([FromQuery] string? status, CancellationToken ct)
    {
        var q = _db.GradeChangeRequests.AsNoTracking().AsQueryable();
        if (Enum.TryParse<GradeChangeStatus>(status, true, out var st)) q = q.Where(r => r.Status == st);
        return Ok(await q.OrderByDescending(r => r.CreatedUtc).Select(r => new GradeChangeDto(
            r.Id, r.EnrollmentId, r.OriginalLetterGrade, r.ProposedLetterGrade, r.ReasonCode.ToString(),
            r.Status.ToString(), r.ProposedBy, r.DecidedBy)).ToListAsync(ct));
    }

    [SMS.Api.Auth.HasPermission("grades.change.propose")]
    [HttpPost("changes")]
    public async Task<ActionResult<object>> Propose(ProposeGradeChangeDto dto, CancellationToken ct)
    {
        try
        {
            var reason = Enum.TryParse<GradeChangeReasonCode>(dto.ReasonCode, true, out var rc) ? rc : GradeChangeReasonCode.Other;
            var r = await _svc.ProposeAsync(dto.EnrollmentId, dto.ProposedLetterGrade, dto.ProposedFinalMark, reason, dto.Reason,
                User.Identity?.Name ?? "proposer", ct);
            return Ok(new { r.Id, r.Status, r.OriginalLetterGrade, r.ProposedLetterGrade });
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [SMS.Api.Auth.HasPermission("grades.change.approve")]
    [HttpPost("changes/{id:int}/approve")]
    public async Task<ActionResult<object>> Approve(int id, CancellationToken ct)
    {
        try { var r = await _svc.ApproveAndApplyAsync(id, User.Identity?.Name ?? "approver", ct);
              return Ok(new { r.Id, r.Status, appliedBatch = r.ApplyBatchId }); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    public record RejectBody(string? Reason);
    [HttpPost("changes/{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, RejectBody b, CancellationToken ct)
    {
        try { await _svc.RejectAsync(id, User.Identity?.Name ?? "approver", b.Reason, ct); return NoContent(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    public record WithholdBody(int? CaseId);
    [HttpPost("enrollments/{enrollmentId:int}/withhold")]
    public async Task<IActionResult> Withhold(int enrollmentId, WithholdBody b, CancellationToken ct)
    {
        try { await _svc.WithholdAsync(enrollmentId, b.CaseId, ct); return NoContent(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("enrollments/{enrollmentId:int}/release")]
    public async Task<IActionResult> Release(int enrollmentId, CancellationToken ct)
    {
        try { await _svc.ReleaseWithheldAsync(enrollmentId, ct); return NoContent(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("pre-publication/{termId:int}")]
    public async Task<ActionResult<PrePublicationReport>> PrePublication(int termId, CancellationToken ct)
        => Ok(await _svc.PrePublicationCheckAsync(termId, ct));
}
