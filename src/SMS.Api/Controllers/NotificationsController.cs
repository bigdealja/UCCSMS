using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMS.Infrastructure.Notifications;

namespace SMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly NotificationService _svc;
    private readonly AlertsService _alerts;
    public NotificationsController(NotificationService svc, AlertsService alerts) { _svc = svc; _alerts = alerts; }

    [HttpGet("inbox")]
    public async Task<ActionResult<object>> Inbox([FromQuery] bool unreadOnly = false, CancellationToken ct = default)
    {
        var items = await _svc.InboxAsync(User.Identity?.Name, unreadOnly, ct);
        return Ok(items.Select(n => new { n.Id, n.Title, n.Body, n.Category, Severity = n.Severity.ToString(),
            n.RefType, n.RefId, n.IsRead, n.CreatedUtc }));
    }

    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> Read(int id, CancellationToken ct) { await _svc.MarkReadAsync(id, ct); return NoContent(); }

    /// <summary>Run alert generation now (overdue cases, exam clashes). Intended to be scheduled;
    /// dedupes so repeated runs don't spam the inbox.</summary>
    [HttpPost("run-alerts")]
    public async Task<ActionResult<object>> RunAlerts(CancellationToken ct)
        => Ok(new { created = await _alerts.GenerateAsync(ct) });
}
