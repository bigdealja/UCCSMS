using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SMS.Api.Auth;
using SMS.Api.Dtos;
using SMS.Application.Hr;
using SMS.Domain.Enums;
using SMS.Infrastructure.Hr;
using SMS.Infrastructure.Persistence;

namespace SMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[HasPermission("payroll.manage")]
public class PayrollController : ControllerBase
{
    private readonly SmsDbContext _db;
    private readonly PayrollService _svc;
    private readonly IHrProvider _hr;
    public PayrollController(SmsDbContext db, PayrollService svc, IHrProvider hr) { _db = db; _svc = svc; _hr = hr; }
    string Me => User.Identity?.Name ?? "staff";

    [HttpGet("hr/health")]
    public async Task<ActionResult<object>> HrHealth(CancellationToken ct)
        => Ok(new { provider = _hr.Name, external = _hr.IsExternal, reachable = _hr.IsExternal ? await _hr.HealthAsync(ct) : (bool?)null });

    [HttpGet("rates")]
    public async Task<ActionResult<IEnumerable<PayRateDto>>> Rates(CancellationToken ct) =>
        Ok(await _db.PayRates.AsNoTracking().OrderBy(r => r.WorkType).ThenByDescending(r => r.EffectiveFrom)
            .Select(r => new PayRateDto(r.Id, r.WorkType.ToString(), r.RatePerUnit, r.Unit, r.EffectiveFrom.ToString("yyyy-MM-dd"))).ToListAsync(ct));

    public record AddRateBody(string WorkType, decimal RatePerUnit, string? Unit, string EffectiveFrom);
    [HttpPost("rates")]
    public async Task<ActionResult<object>> AddRate(AddRateBody b, CancellationToken ct)
    {
        if (!Enum.TryParse<WorkType>(b.WorkType, true, out var wt)) return BadRequest("Invalid work type.");
        if (!DateOnly.TryParse(b.EffectiveFrom, out var d)) return BadRequest("Invalid date.");
        var r = await _svc.AddRateAsync(wt, b.RatePerUnit, b.Unit, d, ct);
        return Ok(new { r.Id });
    }

    static WorkClaimDto Map(Domain.Entities.WorkClaim c) =>
        new(c.Id, c.ClaimantName, c.WorkType.ToString(), c.Units, c.RatePerUnit, c.Amount, c.Status.ToString(), c.SubmittedBy, c.ApprovedBy, c.ExternalRef);

    [HttpGet("claims")]
    public async Task<ActionResult<IEnumerable<WorkClaimDto>>> Claims([FromQuery] string? status, CancellationToken ct)
    {
        var q = _db.WorkClaims.AsNoTracking().AsQueryable();
        if (Enum.TryParse<ClaimStatus>(status, true, out var st)) q = q.Where(c => c.Status == st);
        return Ok(await q.OrderByDescending(c => c.CreatedUtc).Select(c => Map(c)).ToListAsync(ct));
    }

    public record CreateClaimBody(string ClaimantName, int? StaffFacultyId, string WorkType, decimal Units, string Date, string? Evidence);
    [HttpPost("claims")]
    public async Task<ActionResult<object>> Create(CreateClaimBody b, CancellationToken ct)
    {
        if (!Enum.TryParse<WorkType>(b.WorkType, true, out var wt)) return BadRequest("Invalid work type.");
        if (!DateOnly.TryParse(b.Date, out var d)) return BadRequest("Invalid date.");
        try { var c = await _svc.CreateClaimAsync(b.ClaimantName, b.StaffFacultyId, wt, b.Units, d, b.Evidence, ct); return Ok(Map(c)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("claims/{id:int}/submit")]
    public Task<IActionResult> Submit(int id, CancellationToken ct) => Act(() => _svc.SubmitAsync(id, Me, ct));
    [HttpPost("claims/{id:int}/approve")]
    public Task<IActionResult> Approve(int id, CancellationToken ct) => Act(() => _svc.ApproveAsync(id, Me, ct));
    [HttpPost("claims/{id:int}/reject")]
    public Task<IActionResult> Reject(int id, CancellationToken ct) => Act(() => _svc.RejectAsync(id, Me, ct));

    [HttpPost("claims/{id:int}/export")]
    public async Task<ActionResult<object>> Export(int id, CancellationToken ct)
    { try { var c = await _svc.ExportAsync(id, ct); return Ok(new { c.Id, status = c.Status.ToString(), c.ExternalRef, c.Provider }); }
      catch (InvalidOperationException ex) { return BadRequest(ex.Message); } }

    async Task<IActionResult> Act(Func<Task> f)
    { try { await f(); return NoContent(); } catch (InvalidOperationException ex) { return BadRequest(ex.Message); } }
}
