using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SMS.Api.Auth;
using SMS.Api.Dtos;
using SMS.Infrastructure.Persistence;
using SMS.Infrastructure.Scripts;

namespace SMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[HasPermission("scripts.manage")]
public class ScriptsController : ControllerBase
{
    private readonly SmsDbContext _db;
    private readonly ScriptCustodyService _svc;
    public ScriptsController(SmsDbContext db, ScriptCustodyService svc) { _db = db; _svc = svc; }
    string Me => User.Identity?.Name ?? "staff";

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ScriptBundleDto>>> List(CancellationToken ct) =>
        Ok(await _db.ScriptBundles.AsNoTracking().OrderByDescending(b => b.CreatedUtc)
            .Select(b => new ScriptBundleDto(b.Id, b.Code, b.ExamId, b.ScriptCount, b.Status.ToString(), b.Custodian,
                b.RetentionUntil == null ? null : b.RetentionUntil.ToString(), b.DisposalWitness)).ToListAsync(ct));

    [HttpGet("{id:int}/chain")]
    public async Task<ActionResult<object>> Chain(int id, CancellationToken ct)
        => Ok((await _svc.ChainAsync(id, ct)).Select(e => new { e.Action, e.FromCustodian, e.ToCustodian, e.By, e.Notes, e.CreatedUtc }));

    public record CreateBody(int ExamId, string Code, int ScriptCount, string Custodian);
    [HttpPost]
    public async Task<ActionResult<object>> Create(CreateBody b, CancellationToken ct)
    { var x = await _svc.CreateBundleAsync(b.ExamId, b.Code, b.ScriptCount, b.Custodian, ct); return Ok(new { x.Id, status = x.Status.ToString() }); }

    public record HandoverBody(string To);
    [HttpPost("{id:int}/handover")]
    public Task<IActionResult> Handover(int id, HandoverBody b, CancellationToken ct) => Act(() => _svc.HandoverAsync(id, b.To, Me, ct));

    public record ScanBody(int ScannedCount, string? CollectionRef);
    [HttpPost("{id:int}/scan")]
    public async Task<ActionResult<object>> Scan(int id, ScanBody b, CancellationToken ct)
    { try { var s = await _svc.RecordScanAsync(id, b.ScannedCount, b.CollectionRef, Me, ct); return Ok(new { s.Id }); } catch (InvalidOperationException ex) { return BadRequest(ex.Message); } }

    public record MarkerBody(string Marker);
    [HttpPost("{id:int}/issue")]
    public Task<IActionResult> Issue(int id, MarkerBody b, CancellationToken ct) => Act(() => _svc.IssueToMarkerAsync(id, b.Marker, Me, ct));

    public record UploadBody(string FileName, int? DocumentId);
    [HttpPost("{id:int}/marked")]
    public async Task<ActionResult<object>> Marked(int id, UploadBody b, CancellationToken ct)
    { try { var u = await _svc.UploadMarkedAsync(id, b.FileName, b.DocumentId, Me, ct); return Ok(new { u.Id }); } catch (InvalidOperationException ex) { return BadRequest(ex.Message); } }

    public record ReturnBody(string To);
    [HttpPost("{id:int}/return")]
    public Task<IActionResult> Return(int id, ReturnBody b, CancellationToken ct) => Act(() => _svc.ReturnAsync(id, b.To, Me, ct));

    public record RetentionBody(string Until);
    [HttpPost("{id:int}/retention")]
    public async Task<IActionResult> Retention(int id, RetentionBody b, CancellationToken ct)
    { if (!DateOnly.TryParse(b.Until, out var d)) return BadRequest("Invalid date."); return await Act(() => _svc.SetRetentionAsync(id, d, Me, ct)); }

    public record DisposeBody(string Witness);
    [HttpPost("{id:int}/dispose")]
    public Task<IActionResult> Dispose(int id, DisposeBody b, CancellationToken ct) => Act(() => _svc.DisposeAsync(id, b.Witness, Me, ct));

    async Task<IActionResult> Act(Func<Task> f)
    { try { await f(); return NoContent(); } catch (InvalidOperationException ex) { return BadRequest(ex.Message); } }
}
