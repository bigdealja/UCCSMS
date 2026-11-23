using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SMS.Api.Auth;
using SMS.Api.Dtos;
using SMS.Infrastructure.Persistence;

namespace SMS.Api.Controllers;

/// <summary>Read-only audit trail for auditors. The log is append-only; there is no write endpoint.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[HasPermission("audit.read")]
public class AuditController : ControllerBase
{
    private readonly SmsDbContext _db;
    public AuditController(SmsDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<object>> List([FromQuery] string? entityType, [FromQuery] string? entityId,
        [FromQuery] string? actor, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 200); page = Math.Max(1, page);
        var q = _db.AuditEvents.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(entityType)) q = q.Where(a => a.EntityType == entityType);
        if (!string.IsNullOrWhiteSpace(entityId)) q = q.Where(a => a.EntityId == entityId);
        if (!string.IsNullOrWhiteSpace(actor)) q = q.Where(a => a.Actor == actor);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(a => a.Timestamp).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new AuditEventDto(a.Id, a.Timestamp.ToString("o"), a.Actor, a.Action, a.EntityType, a.EntityId, a.Before, a.After, a.Reason))
            .ToListAsync(ct);
        return Ok(new { items, total, page, pageSize });
    }
}
