using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SMS.Api.Dtos;
using SMS.Domain.Entities;
using SMS.Domain.Enums;
using SMS.Infrastructure.Cases;
using SMS.Infrastructure.Persistence;

namespace SMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CasesController : ControllerBase
{
    private readonly SmsDbContext _db;
    private readonly CaseService _svc;
    public CasesController(SmsDbContext db, CaseService svc) { _db = db; _svc = svc; }

    [HttpGet("types")]
    public async Task<ActionResult<IEnumerable<CaseTypeDto>>> Types(CancellationToken ct) =>
        Ok(await _db.CaseTypes.AsNoTracking().Where(t => t.IsActive).OrderBy(t => t.Name)
            .Select(t => new CaseTypeDto(t.Id, t.Code, t.Name, t.TurnaroundWorkingDays, t.RequiresSecondPersonApproval)).ToListAsync(ct));

    static CaseDto Map(Case c, string typeName) => new(c.Id, c.CaseNumber, typeName, c.Title, c.Status.ToString(),
        c.Priority.ToString(), c.StudentId, c.AssignedTo, c.RaisedBy, c.DueUtc.ToString("o"),
        c.ClosedUtc == null && c.DueUtc < DateTimeOffset.UtcNow,                         // overdue
        c.ClosedUtc == null && c.DueUtc >= DateTimeOffset.UtcNow && c.DueUtc < DateTimeOffset.UtcNow.AddDays(2)); // due soon

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CaseDto>>> List(
        [FromQuery] string? status, [FromQuery] string? assignedTo, [FromQuery] bool? overdue, CancellationToken ct)
    {
        var q = _db.Cases.Include(c => c.CaseType).AsNoTracking().AsQueryable();
        if (Enum.TryParse<CaseStatus>(status, true, out var st)) q = q.Where(c => c.Status == st);
        if (!string.IsNullOrWhiteSpace(assignedTo)) q = q.Where(c => c.AssignedTo == assignedTo);
        if (overdue == true) q = q.Where(c => c.ClosedUtc == null && c.DueUtc < DateTimeOffset.UtcNow);
        var items = await q.OrderBy(c => c.DueUtc).ToListAsync(ct);
        return Ok(items.Select(c => Map(c, c.CaseType.Name)));
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<object>> Dashboard(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var open = _db.Cases.Where(c => c.ClosedUtc == null);
        return Ok(new
        {
            open = await open.CountAsync(ct),
            overdue = await open.CountAsync(c => c.DueUtc < now, ct),
            dueSoon = await open.CountAsync(c => c.DueUtc >= now && c.DueUtc < now.AddDays(2), ct),
            pendingApproval = await open.CountAsync(c => c.Status == CaseStatus.PendingApproval, ct),
            byType = await _db.Cases.Include(c => c.CaseType).Where(c => c.ClosedUtc == null)
                .GroupBy(c => c.CaseType.Name).Select(g => new { type = g.Key, count = g.Count() }).ToListAsync(ct)
        });
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<object>> Get(int id, CancellationToken ct)
    {
        var c = await _db.Cases.Include(x => x.CaseType).Include(x => x.Actions).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return NotFound();
        return Ok(new { Case = Map(c, c.CaseType.Name), c.Description, c.Resolution, c.ApprovedBy, c.CancelReason,
            actions = c.Actions.OrderBy(a => a.CreatedUtc).Select(a => new { a.Type, a.Text, a.By, a.CreatedUtc }) });
    }

    [HttpPost]
    public async Task<ActionResult<CaseDto>> Create(CreateCaseDto dto, CancellationToken ct)
    {
        try
        {
            var prio = Enum.TryParse<CasePriority>(dto.Priority, true, out var p) ? p : CasePriority.Normal;
            var c = await _svc.CreateAsync(dto.CaseTypeId, dto.Title, dto.Description, dto.StudentId,
                User.Identity?.Name ?? "staff", prio, ct);
            var typeName = (await _db.CaseTypes.FindAsync(new object[] { dto.CaseTypeId }, ct))!.Name;
            return Ok(Map(c, typeName));
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    public record NoteBody(string Text);
    public record AssignBody(string Assignee);
    public record ResolveBody(string Resolution);
    public record CancelBody(string Reason);
    public record LinkBody(int LinkedCaseId);

    [HttpPost("{id:int}/notes")]
    public Task<IActionResult> Note(int id, NoteBody b, CancellationToken ct) => Act(() => _svc.AddNoteAsync(id, b.Text, User.Identity?.Name ?? "staff", ct));
    [HttpPost("{id:int}/assign")]
    public Task<IActionResult> Assign(int id, AssignBody b, CancellationToken ct) => Act(() => _svc.AssignAsync(id, b.Assignee, User.Identity?.Name ?? "staff", ct));
    [SMS.Api.Auth.HasPermission("cases.approve")]
    [HttpPost("{id:int}/approve")]
    public Task<IActionResult> Approve(int id, CancellationToken ct) => Act(() => _svc.ApproveAsync(id, User.Identity?.Name ?? "approver", ct));
    [HttpPost("{id:int}/resolve")]
    public Task<IActionResult> Resolve(int id, ResolveBody b, CancellationToken ct) => Act(() => _svc.ResolveAsync(id, b.Resolution, User.Identity?.Name ?? "staff", ct));
    [HttpPost("{id:int}/cancel")]
    public Task<IActionResult> Cancel(int id, CancelBody b, CancellationToken ct) => Act(() => _svc.CancelAsync(id, b.Reason, User.Identity?.Name ?? "staff", ct));
    [HttpPost("{id:int}/link")]
    public Task<IActionResult> Link(int id, LinkBody b, CancellationToken ct) => Act(() => _svc.LinkAsync(id, b.LinkedCaseId, User.Identity?.Name ?? "staff", ct));

    async Task<IActionResult> Act(Func<Task> f)
    {
        try { await f(); return NoContent(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }
}
