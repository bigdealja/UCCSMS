using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SMS.Api.Auth;
using SMS.Api.Dtos;
using SMS.Domain.Enums;
using SMS.Infrastructure.Persistence;
using SMS.Infrastructure.QuestionPapers;

namespace SMS.Api.Controllers;

[ApiController]
[Route("api/questionpapers")]
[Authorize]
[HasPermission("papers.manage")]
public class QuestionPapersController : ControllerBase
{
    private readonly SmsDbContext _db;
    private readonly QuestionPaperService _svc;
    public QuestionPapersController(SmsDbContext db, QuestionPaperService svc) { _db = db; _svc = svc; }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PaperDto>>> List([FromQuery] int? termId, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var q = _db.QuestionPapers.AsNoTracking().AsQueryable();
        if (termId is int t) q = q.Where(p => p.TermId == t);
        var items = await q.OrderBy(p => p.SubmissionDeadline).ToListAsync(ct);
        return Ok(items.Select(p => new PaperDto(p.Id, p.Title, p.CourseId, p.TermId, p.Status.ToString(),
            p.SubmissionDeadline.ToString("yyyy-MM-dd"), p.AuthorLecturer, p.ExternalReviewRequired, p.CurrentVersion,
            p.Status == PaperStatus.Expected && p.SubmissionDeadline < today,                                   // overdue
            p.Status == PaperStatus.Expected && p.SubmissionDeadline >= today && p.SubmissionDeadline < today.AddDays(5)))); // at-risk
    }

    public record RegisterBody(int CourseId, int TermId, string Title, string SubmissionDeadline, string? AuthorLecturer, bool ExternalReviewRequired);
    [HttpPost]
    public async Task<ActionResult<object>> Register(RegisterBody b, CancellationToken ct)
    {
        if (!DateOnly.TryParse(b.SubmissionDeadline, out var dl)) return BadRequest("Invalid deadline.");
        var p = await _svc.RegisterAsync(b.CourseId, b.TermId, b.Title, dl, b.AuthorLecturer, b.ExternalReviewRequired, ct);
        return Ok(new { p.Id, status = p.Status.ToString() });
    }

    public record VersionBody(string FileName, int? DocumentId, string? Notes);
    [HttpPost("{id:int}/versions")]
    public async Task<ActionResult<object>> SubmitVersion(int id, VersionBody b, CancellationToken ct)
    { try { var v = await _svc.SubmitVersionAsync(id, b.FileName, b.DocumentId, User.Identity?.Name ?? "lecturer", b.Notes, ct); return Ok(new { v.Id, v.VersionNumber }); }
      catch (InvalidOperationException ex) { return BadRequest(ex.Message); } }

    public record AdvanceBody(string ToStatus);
    [HttpPost("{id:int}/advance")]
    public async Task<ActionResult<object>> Advance(int id, AdvanceBody b, CancellationToken ct)
    {
        if (!Enum.TryParse<PaperStatus>(b.ToStatus, true, out var st)) return BadRequest("Invalid status.");
        try { var p = await _svc.AdvanceAsync(id, st, ct); return Ok(new { p.Id, status = p.Status.ToString() }); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    public record PrintBody(int Copies, int SpareCopies, string? Location, string? SecureStorageRef);
    [HttpPost("{id:int}/print-run")]
    public async Task<ActionResult<object>> PrintRun(int id, PrintBody b, CancellationToken ct)
    { try { var r = await _svc.RecordPrintRunAsync(id, b.Copies, b.SpareCopies, b.Location, b.SecureStorageRef, ct); return Ok(new { printRunId = r.Id }); }
      catch (InvalidOperationException ex) { return BadRequest(ex.Message); } }

    [HttpPost("print-runs/{printRunId:int}/dispatch")]
    public async Task<IActionResult> Dispatch(int printRunId, CancellationToken ct)
    { try { await _svc.DispatchAsync(printRunId, ct); return NoContent(); } catch (InvalidOperationException ex) { return BadRequest(ex.Message); } }

    public record ReturnBody(int ReturnedUnused);
    [HttpPost("print-runs/{printRunId:int}/return")]
    public async Task<IActionResult> Return(int printRunId, ReturnBody b, CancellationToken ct)
    { try { await _svc.RecordReturnAsync(printRunId, b.ReturnedUnused, ct); return NoContent(); } catch (InvalidOperationException ex) { return BadRequest(ex.Message); } }

    public record DestroyBody(string Witness);
    [HttpPost("print-runs/{printRunId:int}/destroy")]
    public async Task<IActionResult> Destroy(int printRunId, DestroyBody b, CancellationToken ct)
    { try { await _svc.DestroyAsync(printRunId, b.Witness, ct); return NoContent(); } catch (InvalidOperationException ex) { return BadRequest(ex.Message); } }

    public record WithdrawBody(string Reason);
    [HttpPost("{id:int}/withdraw")]
    public async Task<IActionResult> Withdraw(int id, WithdrawBody b, CancellationToken ct)
    { try { await _svc.WithdrawAsync(id, b.Reason, ct); return NoContent(); } catch (InvalidOperationException ex) { return BadRequest(ex.Message); } }
}
