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
    private readonly SMS.Infrastructure.Moodle.MoodleGradebookSyncService _gb;
    public MoodleController(MoodleSyncService sync, IMoodleClient client, IOptions<MoodleOptions> opt, SMS.Infrastructure.Moodle.MoodleGradebookSyncService gb)
    { _sync = sync; _client = client; _opt = opt.Value; _gb = gb; }

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

    /// <summary>List the linked Moodle course's grade items (id + name) so each can be mapped to a gradebook component.</summary>
    [HttpGet("grade-items/{courseId:int}")]
    public async Task<ActionResult<object>> GradeItems(int courseId, CancellationToken ct)
    {
        try { return Ok(await _gb.ListItemsAsync(courseId, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>Pull Moodle activity marks into the gradebook for components mapped to Moodle items.</summary>
    [HttpPost("sync-gradebook/{courseId:int}/{termId:int}")]
    public async Task<ActionResult<object>> SyncGradebook(int courseId, int termId, CancellationToken ct)
    {
        try { return Ok(await _gb.SyncAsync(courseId, termId, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }
}
