using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SMS.Api.Auth;
using SMS.Api.Dtos;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Examiners;
using SMS.Infrastructure.Persistence;

namespace SMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[HasPermission("examiners.manage")]
public class ExaminersController : ControllerBase
{
    private readonly SmsDbContext _db;
    private readonly ExaminerService _svc;
    public ExaminersController(SmsDbContext db, ExaminerService svc) { _db = db; _svc = svc; }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ExaminerDto>>> List(CancellationToken ct) =>
        Ok(await _db.ExternalExaminers.AsNoTracking().OrderBy(e => e.LastName)
            .Select(e => new ExaminerDto(e.Id, e.FirstName, e.LastName, e.Email, e.Institution, e.Status.ToString(),
                e.ConflictDeclarationRecorded, e.HasConflict)).ToListAsync(ct));

    [HttpPost]
    public async Task<ActionResult<ExaminerDto>> Register(UpsertExaminerDto dto, CancellationToken ct)
    {
        var e = await _svc.RegisterAsync(new ExternalExaminer { FirstName = dto.FirstName, LastName = dto.LastName,
            Email = dto.Email, Institution = dto.Institution }, ct);
        return Ok(new ExaminerDto(e.Id, e.FirstName, e.LastName, e.Email, e.Institution, e.Status.ToString(), false, false));
    }

    public record ConflictBody(bool HasConflict, string? Details);
    [HttpPost("{id:int}/conflict")]
    public async Task<IActionResult> Conflict(int id, ConflictBody b, CancellationToken ct)
    { try { await _svc.DeclareConflictAsync(id, b.HasConflict, b.Details, ct); return NoContent(); }
      catch (InvalidOperationException ex) { return BadRequest(ex.Message); } }

    [HttpGet("{id:int}/assignments")]
    public async Task<ActionResult<IEnumerable<AssignmentDto>>> Assignments(int id, CancellationToken ct) =>
        Ok(await _db.ExaminerAssignments.AsNoTracking().Where(a => a.ExaminerId == id)
            .Select(a => new AssignmentDto(a.Id, a.ExaminerId, a.ProgrammeId, a.TermId, a.Stage.ToString())).ToListAsync(ct));

    public record CreateAssignmentBody(int? ProgrammeId, int? TermId);
    [HttpPost("{id:int}/assignments")]
    public async Task<ActionResult<object>> CreateAssignment(int id, CreateAssignmentBody b, CancellationToken ct)
    { try { var a = await _svc.CreateAssignmentAsync(id, b.ProgrammeId, b.TermId, ct); return Ok(new { a.Id, a.Stage }); }
      catch (InvalidOperationException ex) { return BadRequest(ex.Message); } }

    public record AdvanceBody(string ToStage);
    [HttpPost("assignments/{assignmentId:int}/advance")]
    public async Task<ActionResult<object>> Advance(int assignmentId, AdvanceBody b, CancellationToken ct)
    {
        if (!Enum.TryParse<AssignmentStage>(b.ToStage, true, out var st)) return BadRequest("Invalid stage.");
        try { var a = await _svc.AdvanceAsync(assignmentId, st, User.Identity?.Name ?? "staff", ct); return Ok(new { a.Id, stage = a.Stage.ToString() }); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    public record ReportBody(string Summary, string? Recommendations);
    [HttpPost("assignments/{assignmentId:int}/report")]
    public async Task<ActionResult<object>> Report(int assignmentId, ReportBody b, CancellationToken ct)
    { try { var r = await _svc.SubmitReportAsync(assignmentId, b.Summary, b.Recommendations, User.Identity?.Name ?? "examiner", ct); return Ok(new { r.Id }); }
      catch (InvalidOperationException ex) { return BadRequest(ex.Message); } }

    public record ActionPlanBody(string Plan, string Owner, string? Corrective);
    [HttpPost("reports/{reportId:int}/action-plan")]
    public async Task<IActionResult> ActionPlan(int reportId, ActionPlanBody b, CancellationToken ct)
    { try { await _svc.AddActionPlanAsync(reportId, b.Plan, b.Owner, b.Corrective, ct); return NoContent(); }
      catch (InvalidOperationException ex) { return BadRequest(ex.Message); } }

    public record TokenBody(int ValidDays);
    [HttpPost("{id:int}/portal-token")]
    public async Task<ActionResult<object>> PortalToken(int id, TokenBody b, CancellationToken ct)
    { try { var t = await _svc.GeneratePortalTokenAsync(id, b.ValidDays <= 0 ? 30 : b.ValidDays, ct);
            return Ok(new { t.Token, t.ExpiresUtc, portalUrl = $"/examiner-portal/{t.Token}" }); }
      catch (InvalidOperationException ex) { return BadRequest(ex.Message); } }
}
