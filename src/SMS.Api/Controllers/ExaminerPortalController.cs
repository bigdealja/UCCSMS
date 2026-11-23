using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMS.Infrastructure.Examiners;

namespace SMS.Api.Controllers;

/// <summary>Public, token-gated examiner portal: an external examiner views their own assignments
/// with a time-limited token, without a full staff account.</summary>
[ApiController]
[Route("api/examinerportal")]
[AllowAnonymous]
public class ExaminerPortalController : ControllerBase
{
    private readonly ExaminerService _svc;
    public ExaminerPortalController(ExaminerService svc) => _svc = svc;

    [HttpGet("{token}")]
    public async Task<ActionResult<object>> Get(string token, CancellationToken ct)
    {
        var r = await _svc.ResolvePortalAsync(token, ct);
        if (r is null) return Unauthorized("Invalid or expired portal link.");
        var (e, assignments) = r.Value;
        return Ok(new { examiner = new { e.FirstName, e.LastName, e.Institution },
            assignments = assignments.Select(a => new { a.Id, a.ProgrammeId, a.TermId, stage = a.Stage.ToString() }) });
    }
}
