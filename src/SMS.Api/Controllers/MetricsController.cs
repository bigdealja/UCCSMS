using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMS.Infrastructure.Notifications;

namespace SMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MetricsController : ControllerBase
{
    private readonly MetricsService _svc;
    public MetricsController(MetricsService svc) => _svc = svc;

    /// <summary>Cross-module KPI snapshot for the executive dashboard.</summary>
    [HttpGet("overview")]
    public async Task<ActionResult<object>> Overview(CancellationToken ct) => Ok(await _svc.OverviewAsync(ct));
}
