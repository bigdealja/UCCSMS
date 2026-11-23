using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SMS.Application.Moodle;
using SMS.Infrastructure.Moodle;

namespace SMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MoodleController : ControllerBase
{
    private readonly MoodleSyncService _sync;
    private readonly IMoodleClient _client;
    private readonly MoodleOptions _opt;
    public MoodleController(MoodleSyncService sync, IMoodleClient client, IOptions<MoodleOptions> opt)
    { _sync = sync; _client = client; _opt = opt.Value; }

    /// <summary>Is Moodle reachable with the configured token?</summary>
    [HttpGet("health")]
    public async Task<ActionResult<object>> Health(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_opt.BaseUrl) || string.IsNullOrWhiteSpace(_opt.Token))
            return Ok(new { configured = false, reachable = false });
        return Ok(new { configured = true, reachable = await _client.PingAsync(ct) });
    }

    /// <summary>Provision a course to Moodle and enrol its registered students (SMS → Moodle).</summary>
    [HttpPost("provision/{courseId:int}/{termId:int}")]
    public async Task<ActionResult<ProvisionResult>> Provision(int courseId, int termId, CancellationToken ct)
        => Ok(await _sync.ProvisionCourseAsync(courseId, termId, _opt.StudentRoleId, ct));

    /// <summary>Pull final grades from Moodle into SMS as one undoable batch (Moodle → SMS).</summary>
    [HttpPost("pull-grades/{courseId:int}/{termId:int}")]
    public async Task<ActionResult<GradePullResult>> PullGrades(int courseId, int termId, CancellationToken ct)
        => Ok(await _sync.PullGradesAsync(courseId, termId, ct));
}
